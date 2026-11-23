namespace QuietNoize.SaveKit.Internal
{
    using System;
    using UnityEngine;

    internal sealed class ProgressReporter
    {
        private readonly Action<float> m_handler;
        private float m_progress;

        internal ProgressReporter(Action<float> handler)
        {
            m_handler = handler;
            Report(0f);
        }

        internal void Report(float progress)
        {
            m_progress = Mathf.Max(
                m_progress,
                Mathf.Clamp01(progress)
            );

            m_handler?.Invoke(m_progress);
        }

        internal void Complete()
        {
            Report(1f);
        }
    }
}