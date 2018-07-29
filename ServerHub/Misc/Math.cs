using MathCore = System.Math;

namespace ServerHub.Misc {
    public static class Math {
        public static int Clamp(int value, int lowerBound, int upperBound) {
            return (value < lowerBound) ? lowerBound : (value > upperBound) ? upperBound : value;
        }

        public static int Ceiling(double a) {
            return (int)MathCore.Ceiling(a);
        }

        public static int Max(int a, int b) {
            return MathCore.Max(a, b);
        }
    }
}