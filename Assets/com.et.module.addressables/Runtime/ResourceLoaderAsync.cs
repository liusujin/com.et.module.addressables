using UnityEngine;

namespace ETModel
{
    [ObjectSystem]
    public class ResourceLoaderAsyncAsyncSystem : UpdateSystem<ResourceLoaderAsync>
    {
        public override void Update(ResourceLoaderAsync self)
        {
            self.Update();
        }
    }

    public class ResourceLoaderAsync : Component
    {
        private ResourceRequest request;
        
        private ETTaskCompletionSource<UnityEngine.Object> tcs;

        public float Progress => this.request.progress;

        public void Update()
        {
            if (!this.request.isDone)
            {
                return;
            }

            ETTaskCompletionSource<UnityEngine.Object> t = tcs;
            t.SetResult(this.request.asset);
        }

        public override void Dispose()
        {
            if (this.IsDisposed)
            {
                return;
            }
            base.Dispose();
        }

        public ETTask<UnityEngine.Object> LoadAsync(string path)
        {
            this.tcs = new ETTaskCompletionSource<UnityEngine.Object>();
            this.request = UnityEngine.Resources.LoadAsync(path);
            return this.tcs.Task;
        }
    }
}