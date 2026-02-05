using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.World
{
    public class ChunkPool
    {
        private readonly Stack<GameObject> pool = new Stack<GameObject>();
        private readonly Transform parent;
        private readonly int layer;
        private int activeCount;

        public ChunkPool(Transform parent, int layer, int prewarmCount = 0)
        {
            this.parent = parent;
            this.layer = layer;

            for (int i = 0; i < prewarmCount; i++)
            {
                var go = CreateChunkObject("PooledChunk");
                go.SetActive(false);
                pool.Push(go);
            }
        }

        public GameObject Get(string name)
        {
            GameObject go;
            if (pool.Count > 0)
            {
                go = pool.Pop();
                go.name = name;
                go.SetActive(true);
            }
            else
            {
                go = CreateChunkObject(name);
            }

            activeCount++;
            Debug.Log($"[ChunkPool] Get: active={activeCount}, pooled={pool.Count}");
            return go;
        }

        public void Return(GameObject go)
        {
            if (go == null) return;

            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Object.Destroy(mf.sharedMesh);
                mf.sharedMesh = null;
            }

            var mc = go.GetComponent<MeshCollider>();
            if (mc != null)
            {
                mc.sharedMesh = null;
            }

            go.transform.SetParent(parent);
            go.SetActive(false);
            pool.Push(go);
            activeCount--;
        }

        public void DestroyAll()
        {
            while (pool.Count > 0)
            {
                var go = pool.Pop();
                if (go != null) Object.Destroy(go);
            }
        }

        private GameObject CreateChunkObject(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.layer = layer;
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<MeshCollider>();
            return go;
        }

        public int PooledCount => pool.Count;
        public int ActiveCount => activeCount;
    }
}
