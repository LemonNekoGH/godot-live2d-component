/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System.Threading;

namespace Live2D.Cubism.Core
{
    /// <summary>
    /// Mirrors Unity CubismTaskableModel: queue a Core update, optionally run it
    /// off the main thread, then join before reading drawables.
    ///
    /// Movie Maker / tests keep <see cref="ExecuteAsync"/> false so dt stays
    /// deterministic. Native/Unity also fall back to a blocking tick when the
    /// previous task has not finished.
    /// </summary>
    public sealed class CubismTaskableModel
    {
        private readonly CubismModel _model;
        private readonly object _gate = new();
        private int _queued;
        private int _running;
        public bool ExecuteAsync { get; set; }
        public bool DidExecute { get; private set; }

        public CubismTaskableModel(CubismModel model)
        {
            _model = model;
        }

        public void StartUpdate()
        {
            DidExecute = false;
            if (!ExecuteAsync)
            {
                _model.UpdateModel();
                DidExecute = true;
                return;
            }

            Interlocked.Exchange(ref _queued, 1);
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
                return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    lock (_gate)
                    {
                        _model.UpdateModel();
                        DidExecute = true;
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _running, 0);
                    if (Interlocked.Exchange(ref _queued, 0) == 1)
                        StartUpdate();
                }
            });
        }

        public void WaitForCompletion()
        {
            if (!ExecuteAsync)
                return;

            var spins = 0;
            while (Volatile.Read(ref _running) != 0 && spins++ < 10_000)
                Thread.Sleep(0);
        }
    }
}
