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
        private readonly ManualResetEventSlim _done = new(true);
        public bool ExecuteAsync { get; set; }

        public CubismTaskableModel(CubismModel model)
        {
            _model = model;
        }

        public void StartUpdate()
        {
            if (!ExecuteAsync)
            {
                lock (_gate)
                {
                    _model.UpdateModel();
                }
                return;
            }

            _done.Reset();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    lock (_gate)
                    {
                        _model.UpdateModel();
                    }
                }
                finally
                {
                    _done.Set();
                }
            });
        }

        public void WaitForCompletion()
        {
            if (!ExecuteAsync)
                return;
            _done.Wait();
        }
    }
}
