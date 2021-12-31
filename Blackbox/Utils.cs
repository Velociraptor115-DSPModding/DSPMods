using System;
using System.Collections.Generic;
using System.Linq;

namespace DysonSphereProgram.Modding.Blackbox
{
  internal static class Utils
  {
		private static long _GCD(long a, long b)
		{
			if (a % b == 0) return b;
			return _GCD(b, a % b);
		}

		private static long _LCM(long a, long b)
		{
			return a * b / _GCD(a, b);
		}

		internal static long GCD(long a, long b) => a > b ? _GCD(a, b) : _GCD(b, a);
		internal static long LCM(long a, long b) => a > b ? _LCM(a, b) : _LCM(b, a);

		internal static long GCD(IEnumerable<long> xs) => xs.Aggregate((long x, long y) => GCD(x, y));

		internal static long LCM(IEnumerable<long> xs) => xs.Aggregate((long x, long y) => LCM(x, y));

		internal static int GCD(IEnumerable<int> xs) => xs.Aggregate((int x, int y) => (int)GCD(x, y));

		internal static int LCM(IEnumerable<int> xs) => xs.Aggregate((int x, int y) => (int)LCM(x, y));
	}
}
