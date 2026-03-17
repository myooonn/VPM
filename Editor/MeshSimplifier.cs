// Ezpzoptimizer - Mesh Simplifier (QEM - Quadric Error Metrics)
// High-quality polygon reduction using Garland-Heckbert style decimation
// Preserves UV seams and silhouette edges for toon-shaded avatars
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace EzPz
{
    public static class MeshSimplifier
    {
        public static int GetTriangleCount(GameObject go)
        {
            if (go == null) return 0;
            int total = 0;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr != null && smr.sharedMesh != null)
                    total += smr.sharedMesh.triangles.Length / 3;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf != null && mf.sharedMesh != null)
                    total += mf.sharedMesh.triangles.Length / 3;
            return total;
        }

        public class SolverInstance
        {
            public Mesh sourceMesh;
            public QEMSolver solver;
            public float lastQuality = -1;
            public Mesh lastResult;

            public SolverInstance(Mesh mesh)
            {
                sourceMesh = mesh;
                solver = new QEMSolver(mesh);
            }

            public Mesh Simplify(float quality)
            {
                if (Mathf.Abs(quality - lastQuality) < 0.001f) return lastResult;
                
                var session = solver.Clone();
                int targetTriCount = Mathf.Max(4, Mathf.RoundToInt(solver.OriginalTriCount * quality));
                session.Decimate(targetTriCount);
                
                lastQuality = quality;
                lastResult = session.BuildResult(sourceMesh);
                return lastResult;
            }
        }

        public static void SaveMeshes(GameObject go)
        {
            if (go == null) return;
            string folder = "Assets/Ezpzoptimizer/Generated/Simplified";
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr != null && smr.sharedMesh != null && !AssetDatabase.Contains(smr.sharedMesh))
                {
                    string safeName = string.Join("_", smr.sharedMesh.name.Split(Path.GetInvalidFileNameChars()));
                    string path = $"{folder}/{safeName}_{System.Guid.NewGuid().ToString().Substring(0, 4)}.asset";
                    AssetDatabase.CreateAsset(smr.sharedMesh, path);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public class QEMSolver
        {
            public int OriginalTriCount => _triCount;

            public QEMSolver(Mesh mesh)
            {
                _vertCount = mesh.vertexCount;
                _pos = mesh.vertices;
                _uvs_for_cost = mesh.uv;
                _normals = mesh.normals;
                _bws = mesh.boneWeights;
                _colors = mesh.colors32;
                _tangents = mesh.tangents;

                _hasUV = _uvs_for_cost != null && _uvs_for_cost.Length == _vertCount;
                _hasNormals = _normals != null && _normals.Length == _vertCount;
                _hasBW = _bws != null && _bws.Length == _vertCount;
                
                var srcTris = mesh.triangles;
                _triCount = srcTris.Length / 3;
                _tris = (int[])srcTris.Clone();

                _collapseTarget = new int[_vertCount];
                for (int i = 0; i < _vertCount; i++) _collapseTarget[i] = i;

                // Adjacency
                _vertTriCounts = new int[_vertCount];
                foreach (int it in _tris) _vertTriCounts[it]++;
                _vertTriIndices = new int[_vertCount];
                int totalAdj = 0;
                for (int i = 0; i < _vertCount; i++) { _vertTriIndices[i] = totalAdj; totalAdj += _vertTriCounts[i]; }
                _vertTriPool = new int[totalAdj];
                int[] currAdj = (int[])_vertTriIndices.Clone();
                for (int t = 0; t < _triCount; t++)
                {
                    _vertTriPool[currAdj[_tris[t * 3]]++] = t;
                    _vertTriPool[currAdj[_tris[t * 3 + 1]]++] = t;
                    _vertTriPool[currAdj[_tris[t * 3 + 2]]++] = t;
                }

                _isBorder = new bool[_vertCount];
                _isUVSeam = new bool[_vertCount];
                DetectBordersAndSeams();

                _quadrics = new double[_vertCount * 10];
                ComputeQuadrics();
            }

            public QEMSolver Clone()
            {
                var copy = (QEMSolver)this.MemberwiseClone();
                copy._tris = (int[])_tris.Clone();
                copy._pos = (Vector3[])_pos.Clone();
                copy._collapseTarget = (int[])_collapseTarget.Clone();
                copy._quadrics = (double[])_quadrics.Clone();
                copy._vertTriPool = (int[])_vertTriPool.Clone();
                return copy;
            }

            Vector3[] _pos;
            int[] _tris;
            int _triCount;
            int _vertCount;
            double[] _quadrics;
            bool[] _isBorder;
            bool[] _isUVSeam;
            int[] _collapseTarget;
            int[] _vertTriIndices;
            int[] _vertTriCounts;
            int[] _vertTriPool;
            Vector2[] _uvs_for_cost;
            Vector3[] _normals;
            BoneWeight[] _bws;
            Vector4[] _tangents;
            Color32[] _colors;
            bool _hasUV, _hasNormals, _hasBW;

            // Snap positioning to group coincident vertices effectively
            struct VertPosHash {
                public int x, y, z;
                public override int GetHashCode() => (x * 73856093) ^ (y * 19349663) ^ (z * 83492791);
                public override bool Equals(object obj) => obj is VertPosHash other && x == other.x && y == other.y && z == other.z;
            }
            VertPosHash GetPosHash(Vector3 p) => new VertPosHash { x = Mathf.RoundToInt(p.x * 10000f), y = Mathf.RoundToInt(p.y * 10000f), z = Mathf.RoundToInt(p.z * 10000f) };

            void DetectBordersAndSeams()
            {
                // 1. Edge-based border detection
                var edgeCounts = new Dictionary<long, int>();
                for (int t = 0; t < _triCount; t++)
                {
                    CountEdge(edgeCounts, _tris[t * 3], _tris[t * 3 + 1]);
                    CountEdge(edgeCounts, _tris[t * 3 + 1], _tris[t * 3 + 2]);
                    CountEdge(edgeCounts, _tris[t * 3 + 2], _tris[t * 3]);
                }
                foreach (var kv in edgeCounts) if (kv.Value == 1) { long k = kv.Key; _isBorder[(int)(k >> 32)] = true; _isBorder[(int)(k & 0xFFFFFFFF)] = true; }

                // 2. Global Coincidence-based Seam Detection
                // We group ALL vertices by geometric position to find "cracks" between submeshes/UV patches
                var groups = new Dictionary<VertPosHash, List<int>>();
                for (int i = 0; i < _vertCount; i++)
                {
                    var hash = GetPosHash(_pos[i]);
                    if (!groups.TryGetValue(hash, out var list)) groups[hash] = list = new List<int>();
                    list.Add(i);
                }

                foreach (var group in groups.Values)
                {
                    if (group.Count < 2) continue;
                    // Check if there's any difference in non-geometric data
                    bool isStrictSeam = false;
                    int firstIdx = group[0];
                    for (int i = 1; i < group.Count; i++)
                    {
                        int currIdx = group[i];
                        if (_hasUV && (_uvs_for_cost[firstIdx] - _uvs_for_cost[currIdx]).sqrMagnitude > 1e-8f) isStrictSeam = true;
                        if (_hasNormals && Vector3.Angle(_normals[firstIdx], _normals[currIdx]) > 1.0f) isStrictSeam = true;
                        if (_hasBW) {
                            var b1 = _bws[firstIdx]; var b2 = _bws[currIdx];
                            if (b1.boneIndex0 != b2.boneIndex0 || Mathf.Abs(b1.weight0 - b2.weight0) > 0.01f) isStrictSeam = true;
                        }
                        if (isStrictSeam) break;
                    }
                    if (isStrictSeam) foreach (int idx in group) _isUVSeam[idx] = true;
                }
            }

            long EncodeEdge(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            void CountEdge(Dictionary<long, int> d, int a, int b) { long k = EncodeEdge(a, b); if (!d.ContainsKey(k)) d[k] = 0; d[k]++; }

            void ComputeQuadrics()
            {
                for (int t = 0; t < _triCount; t++)
                {
                    int i0 = _tris[t * 3], i1 = _tris[t * 3 + 1], i2 = _tris[t * 3 + 2];
                    Vector3 v0 = _pos[i0], v1 = _pos[i1], v2 = _pos[i2];
                    Vector3 n = Vector3.Cross(v1 - v0, v2 - v0); float area = n.magnitude;
                    if (area < 1e-12f) continue;
                    n /= area; area *= 0.5f;
                    double nx = n.x, ny = n.y, nz = n.z; double d = -(nx * v0.x + ny * v0.y + nz * v0.z); double w = area;
                    double[] q = { nx * nx * w, nx * ny * w, nx * nz * w, nx * d * w, ny * ny * w, ny * nz * w, ny * d * w, nz * nz * w, nz * d * w, d * d * w };
                    AddQ(i0, q); AddQ(i1, q); AddQ(i2, q);
                }
                for (int i = 0; i < _vertCount; i++)
                {
                    double factor = 1.0;
                    if (_isBorder[i]) factor = 1e6; else if (_isUVSeam[i]) factor = 1e5;
                    if (factor > 1.0) for (int j = 0; j < 10; j++) _quadrics[i * 10 + j] *= factor;
                }
            }
            void AddQ(int v, double[] q) { int s = v * 10; for (int i = 0; i < 10; i++) _quadrics[s + i] += q[i]; }

            bool IsFlipping(int v0, int v1, Vector3 nPos)
            {
                int adjCount = _vertTriCounts[v1]; int start = _vertTriIndices[v1];
                for (int k = 0; k < adjCount; k++)
                {
                    int t = _vertTriPool[start + k]; if (_tris[t * 3] < 0) continue;
                    int i0 = Resolve(_tris[t * 3]), i1 = Resolve(_tris[t * 3 + 1]), i2 = Resolve(_tris[t * 3 + 2]);
                    if (i0 == v1) i0 = v0; if (i1 == v1) i1 = v0; if (i2 == v1) i2 = v0;
                    if (i0 == i1 || i1 == i2 || i2 == i0) continue;
                    Vector3 oN = Vector3.Cross(_pos[Resolve(_tris[t * 3 + 1])] - _pos[Resolve(_tris[t * 3])], _pos[Resolve(_tris[t * 3 + 2])] - _pos[Resolve(_tris[t * 3])]).normalized;
                    Vector3 va = i0 == v0 ? nPos : _pos[i0], vb = i1 == v0 ? nPos : _pos[i1], vc = i2 == v0 ? nPos : _pos[i2];
                    Vector3 nN = Vector3.Cross(vb - va, vc - va).normalized;
                    if (Vector3.Dot(oN, nN) < 0.8f) return true;
                }
                return false;
            }

            double GetCost(int v0, int v1, out Vector3 opt)
            {
                double[] q = new double[10]; int s0 = v0 * 10, s1 = v1 * 10;
                for (int i = 0; i < 10; i++) q[i] = _quadrics[s0 + i] + _quadrics[s1 + i];
                Vector3 mid = (_pos[v0] + _pos[v1]) * 0.5f;
                double cMid = EvalQ(q, mid), c0 = EvalQ(q, _pos[v0]), c1 = EvalQ(q, _pos[v1]);
                double bestC = cMid; opt = mid;
                if (c0 < bestC) { bestC = c0; opt = _pos[v0]; }
                if (c1 < bestC) { bestC = c1; opt = _pos[v1]; }
                if (_hasUV) bestC += (_uvs_for_cost[v0] - _uvs_for_cost[v1]).sqrMagnitude * 1e5;
                return bestC;
            }
            double EvalQ(double[] q, Vector3 v) { double x = v.x, y = v.y, z = v.z; return q[0]*x*x + 2*q[1]*x*y + 2*q[2]*x*z + 2*q[3]*x + q[4]*y*y + 2*q[5]*y*z + 2*q[6]*y + q[7]*z*z + 2*q[8]*z + q[9]; }

            int Resolve(int v) { int r = v; while (_collapseTarget[r] != r) r = _collapseTarget[r]; while (_collapseTarget[v] != r) { int n = _collapseTarget[v]; _collapseTarget[v] = r; v = n; } return r; }

            public void Decimate(int targetTriCount)
            {
                int liveTris = _triCount;
                var edges = new List<EdgeCandidate>(); var edgeSet = new HashSet<long>();
                for (int t = 0; t < _triCount; t++) {
                    TryAddEdge(edges, edgeSet, _tris[t * 3], _tris[t * 3 + 1]);
                    TryAddEdge(edges, edgeSet, _tris[t * 3 + 1], _tris[t * 3 + 2]);
                    TryAddEdge(edges, edgeSet, _tris[t * 3 + 2], _tris[t * 3]);
                }
                edges.Sort((a,b) => a.cost.CompareTo(b.cost));
                int iter = 0;
                while (liveTris > targetTriCount && iter < 100)
                {
                    double threshold = 1e-9 * Math.Pow(iter + 3, 20.0);
                    int cur = 0; bool any = false;
                    while (liveTris > targetTriCount && cur < edges.Count)
                    {
                        var e = edges[cur++]; if (e.cost > threshold) break;
                        int v0 = Resolve(e.v0), v1 = Resolve(e.v1); if (v0 == v1) continue;
                        Vector3 opt; double cost = GetCost(v0, v1, out opt);
                        if (cost > threshold || IsFlipping(v0, v1, opt)) continue;
                        _pos[v0] = opt; _collapseTarget[v1] = v0;
                        int s0 = v0 * 10, s1 = v1 * 10; for (int i = 0; i < 10; i++) _quadrics[s0+i] += _quadrics[s1+i];
                        if (_isBorder[v1]) _isBorder[v0] = true; if (_isUVSeam[v1]) _isUVSeam[v0] = true;
                        int adjC = _vertTriCounts[v1], start = _vertTriIndices[v1];
                        for (int k = 0; k < adjC; k++) {
                            int t = _vertTriPool[start + k]; if (_tris[t * 3] < 0) continue;
                            int ta = Resolve(_tris[t * 3]), tb = Resolve(_tris[t * 3 + 1]), tc = Resolve(_tris[t * 3 + 2]);
                            if (ta == v1) ta = v0; if (tb == v1) tb = v0; if (tc == v1) tc = v0;
                            _tris[t * 3] = ta; _tris[t * 3 + 1] = tb; _tris[t * 3 + 2] = tc;
                            if (ta == tb || tb == tc || ta == tc) { _tris[t * 3] = -1; liveTris--; }
                        }
                        any = true;
                    }
                    if (!any) iter++;
                    if (cur >= edges.Count && liveTris > targetTriCount) iter++;
                }
            }

            void TryAddEdge(List<EdgeCandidate> l, HashSet<long> s, int a, int b) {
                if (!s.Add(EncodeEdge(a, b))) return;
                if ((_isBorder[a] && _isBorder[b]) || (_isUVSeam[a] && _isUVSeam[b])) return;
                Vector3 opt; double cost = GetCost(a, b, out opt);
                if (_isBorder[a] || _isBorder[b] || _isUVSeam[a] || _isUVSeam[b]) cost *= 1e6;
                l.Add(new EdgeCandidate { v0 = a, v1 = b, cost = cost });
            }

            struct EdgeCandidate { public int v0, v1; public double cost; }

            public Mesh BuildResult(Mesh src)
            {
                var used = new List<int>(); var remap = new Dictionary<int, int>(); var lTris = new List<int>();
                for (int t = 0; t < _triCount; t++) {
                    if (_tris[t * 3] < 0) continue;
                    int a = Resolve(_tris[t * 3]), b = Resolve(_tris[t * 3 + 1]), c = Resolve(_tris[t * 3 + 2]);
                    if (a == b || b == c || a == c) continue;
                    if (!remap.ContainsKey(a)) { remap[a] = used.Count; used.Add(a); }
                    if (!remap.ContainsKey(b)) { remap[b] = used.Count; used.Add(b); }
                    if (!remap.ContainsKey(c)) { remap[c] = used.Count; used.Add(c); }
                    lTris.Add(remap[a]); lTris.Add(remap[b]); lTris.Add(remap[c]);
                }
                int n = used.Count; if (lTris.Count < 3) return null;
                var pos = new Vector3[n]; var nrm = _hasNormals ? new Vector3[n] : null; var uv = _hasUV ? new Vector2[n] : null;
                var bw = _hasBW ? new BoneWeight[n] : null; var col = (_colors != null && _colors.Length == _vertCount) ? new Color32[n] : null;
                for (int i = 0; i < n; i++) {
                    int oi = used[i]; pos[i] = _pos[oi];
                    if (nrm != null) nrm[i] = _normals[oi]; if (uv != null) uv[i] = _uvs_for_cost[oi]; 
                    if (bw != null) bw[i] = _bws[oi]; if (col != null) col[i] = _colors[oi];
                }
                var d = new Mesh { name = src.name + "_S" };
                d.indexFormat = n > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                d.vertices = pos; if (nrm != null) d.normals = nrm; if (uv != null) d.uv = uv; if (bw != null) d.boneWeights = bw; if (col != null) d.colors32 = col;
                d.bindposes = src.bindposes; d.subMeshCount = 1; d.SetTriangles(lTris.ToArray(), 0);
                d.RecalculateBounds(); if (nrm == null) d.RecalculateNormals();
                return d;
            }
        }
    }
}
