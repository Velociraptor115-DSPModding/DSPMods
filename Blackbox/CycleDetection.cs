using System;
using System.Collections.Generic;

namespace DysonSphereProgram.Modding.Blackbox
{
  public class CycleDetection
  {
    public static IEnumerable<int> DetectCycleLengthsFromEnd(int endIndex, int beginIndex, int stride, Func<int, int, bool> indexEquals)
    {
      for (int i = endIndex - stride; i >= beginIndex; i -= stride)
      {
        if (indexEquals(endIndex, i))
          yield return endIndex - i;
      }
    }

    public static bool VerifyKIterationsFromEnd(int endIndex, int beginIndex, int stride, Func<int, int, bool> indexEquals, int k)
    {
      var numIters = 0;
      for (int i = endIndex - stride; i >= beginIndex; i -= stride)
      {
        for (int j = 0; j < stride; j++)
        {
          if (i - j < beginIndex)
            return false;
          if (!indexEquals(endIndex - j, i - j))
            return false;
        }
        numIters++;
        if (numIters >= k)
          return true;
      }
      return false;
    }

    public static bool TryDetectCycles(int endIndex, int beginIndex, int verificationCount, Func<int, int, bool> indexEquals, out int cycleLength)
    {
      cycleLength = -1;
      var potentialCycleLengths = DetectCycleLengthsFromEnd(endIndex, beginIndex, 1, indexEquals);

      foreach (var potentialCycleLength in potentialCycleLengths)
      {
        if (VerifyKIterationsFromEnd(endIndex, beginIndex, potentialCycleLength, indexEquals, verificationCount))
        {
          cycleLength = potentialCycleLength;
          return true;
        }
      }
      return false;
    }

    public static bool VerifyKIterationsFromEnd(int endIndex, int beginIndex, int stride, Func<int, int, bool> indexEquals, Func<int, int, int, bool> summarizeEquals, int k)
    {
      var numIters = 0;
      for (int i = endIndex - stride; i - stride >= beginIndex; i -= stride)
      {
        if (!summarizeEquals(endIndex, i, stride))
          return false;
        numIters++;
        if (numIters >= k)
          return true;
      }
      return false;
    }

    public static bool TryDetectCycles(int endIndex, int beginIndex, int verificationCount, Func<int, int, bool> indexEquals, Func<int, int, int, bool> summarizeEquals, out int cycleLength)
    {
      cycleLength = -1;
      var potentialCycleLengths = DetectCycleLengthsFromEnd(endIndex, beginIndex, 1, indexEquals);

      foreach (var potentialCycleLength in potentialCycleLengths)
      {
        if (VerifyKIterationsFromEnd(endIndex, beginIndex, potentialCycleLength, indexEquals, summarizeEquals, verificationCount))
        {
          cycleLength = potentialCycleLength;
          return true;
        }
      }
      return false;
    }
  }
}