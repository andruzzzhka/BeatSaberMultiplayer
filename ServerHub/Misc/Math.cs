using System.Linq;
using MathCore = System.Math;

namespace ServerHub.Misc {
    public static class Math {
        public static int Clamp(int value, int lowerBound, int upperBound) {
            return (value < lowerBound) ? lowerBound : (value > upperBound) ? upperBound : value;
        }

        public static int Ceiling(double a)
        {
            return (int)MathCore.Ceiling(a);
        }

        public static int Floor(double a)
        {
            return (int)MathCore.Floor(a);
        }

        public static int Max(int a, int b)
        {
            return MathCore.Max(a, b);
        }

        public static int Min(int a, int b)
        {
            return MathCore.Min(a, b);
        }

        public static float Median(float[] xs)
        {
            if (xs.Length > 0)
            {
                var ys = xs.OrderBy(x => x).ToList();
                double mid = (ys.Count - 1) / 2.0;
                return (ys[(int)(mid)] + ys[(int)(mid + 0.5)]) / 2;
            }
            else
            {
                return 0f;
            }
        }
    }
}