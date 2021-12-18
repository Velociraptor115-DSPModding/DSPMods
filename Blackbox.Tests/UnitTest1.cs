using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DysonSphereProgram.Modding.Blackbox.Tests
{
  class IntSummer : ISummarizer<int>
  {
    public void Initialize(Span<int> data)
    {
      for (int i = 0; i < data.Length; i++)
        data[i] = 0;
    }

    public void Summarize(Span<int> detailed, Span<int> summary)
    {
      for (int i = 0; i < summary.Length; i++)
        summary[i] += detailed[i];
    }
  }

  public class TimeSeriesDataTests
  {
    [Fact]
    public void Test1()
    {
      var mlg = new MultiLevelGranularity();
      mlg.levels = 3;
      mlg.entryCounts = new[] { 12, 12, 12 };
      mlg.ratios = new[] { 4, 3 };

      var ts = new TimeSeriesData<int>(2, mlg, new IntSummer());
      var timeIdx = 0;
      int val = 0;

      for (int iter = 0; iter < 2 * mlg.entryCounts[2] * mlg.ratios[1] * mlg.entryCounts[1] * mlg.ratios[0] * mlg.entryCounts[0]; iter++)
      {
        int l3sum0 = 0;
        int l3sum1 = 0;
        for (int i = 0; i < mlg.ratios[1]; i++)
        {
          int l2sum0 = 0;
          int l2sum1 = 0;
          for (int j = 0; j < mlg.ratios[0]; j++)
          {
            var entry = ts.LevelEntryOffset(0, timeIdx);
            entry[0] = val++;
            entry[1] = val++;
            ts.SummarizeAtHigherGranularity(timeIdx);

            l2sum0 += entry[0];
            l2sum1 += entry[1];
            l3sum0 += entry[0];
            l3sum1 += entry[1];
            timeIdx++;
          }
          Assert.Equal(l2sum0, ts.LevelEntryOffset(1, timeIdx - 1)[0]);
          Assert.Equal(l2sum1, ts.LevelEntryOffset(1, timeIdx - 1)[1]);
        }
        Assert.Equal(l3sum0, ts.LevelEntryOffset(2, timeIdx - 1)[0]);
        Assert.Equal(l3sum1, ts.LevelEntryOffset(2, timeIdx - 1)[1]);
      }
    }
  }
}