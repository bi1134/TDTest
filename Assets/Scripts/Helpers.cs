    using System.Collections.Generic;
    using UnityEngine;

    public class Helpers : MonoBehaviour
    {
        static readonly Dictionary<float, WaitForSeconds> WaitForSeconds = new();

        public static WaitForSeconds GetWaitForSecond(float seconds)
        {
            if (WaitForSeconds.TryGetValue(seconds, out var forSeconds))
                return forSeconds;

            var waitForSeconds = new WaitForSeconds(seconds);
            WaitForSeconds.Add(seconds, waitForSeconds);
            return WaitForSeconds[seconds];
        }
    }
