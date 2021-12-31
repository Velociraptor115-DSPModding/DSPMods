using System;

namespace DysonSphereProgram.Modding.Blackbox
{
  public struct MultiLevelGranularity
  {
    public int levels;

    // entryCounts[levels]
    public int[] entryCounts;

    // ratios[levels - 1]
    // An entry at level i encapsulates `ratio[i]` entries at level i-1
    public int[] ratios;
  }

  public interface ISummarizer<T>
  {
    void Initialize(Span<T> data);
    void Summarize(Span<T> detailed, Span<T> summary);
  }

  public class TimeSeriesData<T> where T : struct
  {
    // data consists of multiple circular buffers that store the time series data 
    // at various levels of granularity
    T[] data;
    MultiLevelGranularity multiLevelGranularity;
    int dataSize;
    ISummarizer<T> summarizer;

    public TimeSeriesData(int dataSize, MultiLevelGranularity mlg, ISummarizer<T> summarizer)
    {
      this.dataSize = dataSize;
      this.multiLevelGranularity = mlg;
      this.summarizer = summarizer;
      var totalEntriesRequired = 0;
      for (int i = 0; i < mlg.entryCounts.Length; i++)
      {
        totalEntriesRequired += mlg.entryCounts[i];
      }
      data = new T[dataSize * totalEntriesRequired];
    }

    public T[] Data => data;
    public int DataSize => dataSize;

    public ref struct LevelSpan<U>
    {
      public int dataSize;
      public Span<U> levelOffset;

      public Span<U> Entry(int entry)
      {
        return levelOffset.Slice(entry * dataSize, dataSize);
      }

      public Span<U> Entries(int startEntry, int count)
      {
        return levelOffset.Slice(startEntry * dataSize, count * dataSize);
      }
    }

    public int LevelOffsetInt(int level)
    {
      // level should be < multiLevelGranularity.levels
      var levelOffset = 0;
      for (int i = 0; i < level; i++)
        levelOffset += multiLevelGranularity.entryCounts[i];

      return levelOffset;
    }

    public int EntryOffsetIntFromBaseTimeIdx(int level, int baseTimeIdx)
    {
      var entryOffsetRaw = baseTimeIdx;
      for (int i = 1; i <= level; i++)
        entryOffsetRaw /= multiLevelGranularity.ratios[i - 1];
      var entryOffset = (entryOffsetRaw % multiLevelGranularity.entryCounts[level]);
      return entryOffset;
    }

    public LevelSpan<T> Level(int level) => new LevelSpan<T>
    {
      dataSize = dataSize,
      levelOffset = new Span<T>(data, LevelOffsetInt(level) * dataSize, multiLevelGranularity.entryCounts[level] * dataSize)
    };

    public Span<T> LevelOffset(int level) => new Span<T>(data, LevelOffsetInt(level) * dataSize, multiLevelGranularity.entryCounts[level] * dataSize);
    public Span<T> LevelEntryOffset(int level, int baseTimeIdx) => Level(level).Entry(EntryOffsetIntFromBaseTimeIdx(level, baseTimeIdx));

    public void SummarizeAtHigherGranularity(int baseTimeIdx)
    {
      var prevLevelNumEntriesMade = baseTimeIdx + 1;
      for (int i = 1; i < multiLevelGranularity.levels; i++)
      {
        if (prevLevelNumEntriesMade % multiLevelGranularity.ratios[i - 1] == 0)
          SummarizeAtGranularity(i, baseTimeIdx);
        else
          break;
        prevLevelNumEntriesMade /= multiLevelGranularity.ratios[i - 1];
      }
    }

    void SummarizeAtGranularity(int level, int baseTimeIdx)
    {
      if (CalculateLowerLevelRange(level, baseTimeIdx, out Span<T> prevLevelSpan, out int count))
      {
        var curLevelEntry = LevelEntryOffset(level, baseTimeIdx);
        summarizer.Initialize(curLevelEntry);
        for (int i = 0; i < count; i++)
          summarizer.Summarize(prevLevelSpan.Slice(i * dataSize, dataSize), curLevelEntry);
      }
    }

    bool CalculateLowerLevelRange(int level, int baseTimeIdx, out Span<T> prevLevelSpan, out int count)
    {
      prevLevelSpan = Span<T>.Empty;
      count = 0;
      if (level == 0) return false;

      count = multiLevelGranularity.ratios[level - 1];
      var prevLevelEntryOffset = EntryOffsetIntFromBaseTimeIdx(level - 1, baseTimeIdx);
      prevLevelSpan = Level(level - 1).Entries(prevLevelEntryOffset - (prevLevelEntryOffset % count), count);
      return true;
    }
  }
}