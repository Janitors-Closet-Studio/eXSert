using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;

#endif

namespace TinyGiantStudio.BetterInspector
{
    /// <summary>
    /// This script serves two purposes.
    /// 1. Gather information about mesh
    /// 2. Perform the actions in the Action Foldout in the inspector
    /// </summary>
    public static class MeshExtension
    {
        #region Information

        public static int[] SubMeshVertexCount(this Mesh mesh)
        {
            if (mesh == null)
                return null;

            int[] vertices = new int[mesh.subMeshCount];
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                vertices[i] = mesh.GetSubMesh(i).vertexCount;
            }

            return vertices;
        }


        public static int TrianglesCount(this Mesh mesh) => mesh.triangles.Length / 3;

        public static int EdgeCount(this Mesh mesh)
        {
            HashSet<Edge> uniqueEdges = new();
            int[] triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Edge edge1 = new(triangles[i], triangles[i + 1]);
                Edge edge2 = new(triangles[i + 1], triangles[i + 2]);
                Edge edge3 = new(triangles[i + 2], triangles[i]);

                uniqueEdges.Add(edge1);
                uniqueEdges.Add(edge2);
                uniqueEdges.Add(edge3);
            }

            return uniqueEdges.Count;
        }

        readonly struct Edge : IEquatable<Edge>
        {
            readonly int _vertexIndexA;
            readonly int _vertexIndexB;

            public Edge(int vertexIndexA, int vertexIndexB)
            {
                _vertexIndexA = Mathf.Min(vertexIndexA, vertexIndexB);
                _vertexIndexB = Mathf.Max(vertexIndexA, vertexIndexB);
            }

            public override int GetHashCode() => _vertexIndexA.GetHashCode() ^ _vertexIndexB.GetHashCode();

            public override bool Equals(object obj)
            {
                if (obj is not Edge other) return false;
                return _vertexIndexA == other._vertexIndexA && _vertexIndexB == other._vertexIndexB;
            }

            public bool Equals(Edge other) =>
                _vertexIndexA == other._vertexIndexA && _vertexIndexB == other._vertexIndexB;
        }

        public static int FaceCount(this Mesh mesh) => mesh.triangles.Length / 3;

#if UNITY_EDITOR

        public static Bounds MeshSizeEditorOnly(this Mesh mesh, float unit = 1)
        {
            Bounds newBound = mesh.bounds;
            newBound.size *= unit;
            newBound.center *= unit;

            return newBound;
        }

#endif

        #endregion Information

        #region Functions

        /// <summary>
        /// Flips the direction of the normals
        /// </summary>
        public static Mesh FlipNormals(this Mesh mesh)
        {
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = -normals[i];
            }

            mesh.normals = normals;

            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // int temp = triangles[i];
                // triangles[i] = triangles[i + 2];
                // triangles[i + 2] = temp;
                (triangles[i], triangles[i + 2]) = (triangles[i + 2], triangles[i]);
            }

            mesh.triangles = triangles;
            return mesh;
        }

#if UNITY_EDITOR

        /// <summary>
        /// Editor only
        /// </summary>
        public static Mesh ExportMesh(this Mesh mesh)
        {
            string path = EditorUtility.SaveFilePanel("Save mesh", "Assets/", mesh.name, "asset");
            if (string.IsNullOrEmpty(path)) return mesh;

            path = FileUtil.GetProjectRelativePath(path);

            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh)))
                mesh = Object.Instantiate(mesh);

            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();

            return mesh;
        }

#endif

        //This is working. Commented out because it was unused.
        // public static Mesh SubDivide(this Mesh mesh)
        // {
        //     Vector3[] vertices = mesh.vertices;
        //     int[] triangles = mesh.triangles;
        //     Vector3[] normals = mesh.normals;
        //     Vector2[] uv = mesh.uv;
        //
        //     int newTriangleCount = triangles.Length * 4;
        //     int[] newTriangles = new int[newTriangleCount];
        //     Vector3[] newVertices = new Vector3[vertices.Length * 4];
        //     Vector3[] newNormals = new Vector3[normals.Length * 4];
        //     Vector2[] newUV = new Vector2[uv.Length * 4];
        //
        //     int triangleIndex = 0;
        //     int vertexIndex = 0;
        //
        //     for (int i = 0; i < triangles.Length; i += 3)
        //     {
        //         int a = triangles[i];
        //         int b = triangles[i + 1];
        //         int c = triangles[i + 2];
        //
        //         Vector3 ab = (vertices[a] + vertices[b]) * 0.5f;
        //         Vector3 bc = (vertices[b] + vertices[c]) * 0.5f;
        //         Vector3 ca = (vertices[c] + vertices[a]) * 0.5f;
        //
        //         newVertices[vertexIndex] = vertices[a];
        //         newVertices[vertexIndex + 1] = vertices[b];
        //         newVertices[vertexIndex + 2] = vertices[c];
        //         newVertices[vertexIndex + 3] = ab;
        //         newVertices[vertexIndex + 4] = bc;
        //         newVertices[vertexIndex + 5] = ca;
        //
        //         newTriangles[triangleIndex] = vertexIndex;
        //         newTriangles[triangleIndex + 1] = vertexIndex + 3;
        //         newTriangles[triangleIndex + 2] = vertexIndex + 5;
        //
        //         newTriangles[triangleIndex + 3] = vertexIndex + 1;
        //         newTriangles[triangleIndex + 4] = vertexIndex + 4;
        //         newTriangles[triangleIndex + 5] = vertexIndex + 3;
        //
        //         newTriangles[triangleIndex + 6] = vertexIndex + 2;
        //         newTriangles[triangleIndex + 7] = vertexIndex + 5;
        //         newTriangles[triangleIndex + 8] = vertexIndex + 4;
        //
        //         newTriangles[triangleIndex + 9] = vertexIndex + 3;
        //         newTriangles[triangleIndex + 10] = vertexIndex + 4;
        //         newTriangles[triangleIndex + 11] = vertexIndex + 5;
        //
        //         triangleIndex += 12;
        //         vertexIndex += 6;
        //     }
        //
        //     mesh.vertices = newVertices;
        //     mesh.triangles = newTriangles;
        //     mesh.normals = newNormals;
        //     mesh.uv = newUV;
        //
        //     mesh.RecalculateNormals();
        //     mesh.RecalculateBounds();
        //
        //     return mesh;
        // }

        #endregion Functions
    }
}