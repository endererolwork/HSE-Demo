using System;

namespace HSE.Clinical
{
    /// <summary>Decision-point variants from the GDD, chosen by the instructor at the entrance.</summary>
    public enum Branch
    {
        Standard,
        WrongWristband,
        CriticalBloodPressure,
        ConsentRefused,
    }

    public static class TrainingBranch
    {
        public static Branch Current { get; private set; } = Branch.Standard;
        public static event Action<Branch> Changed;

        public static void Set(Branch b)
        {
            Current = b;
            Changed?.Invoke(b);
        }
    }
}