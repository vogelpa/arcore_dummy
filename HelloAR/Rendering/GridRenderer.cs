using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Opengl;
using Android.Telecom;
using Google.AR.Core;
using Java.Nio;
using UnityEngine;

/*
namespace HelloAR
{
    public class GridRenderer : MonoBehaviour
    {
        // Mesh to be rendered
        private Mesh mesh;

        // Grid properties
        private float cellSize = 1f; // size of each cell in the grid
        private int gridSize = 10; // number of cells in the grid

        // List to store the vertices of the grid
        private List<Vector3> vertices;

        // List to store the triangles of the grid
        private List<int> triangles;
        
        // Lists to keep track of the indices of changed cells
        private List<int> changedCells = new List<int>();

        // Reference to the ARPointCloud to access point data
        private PointCloud pointCloud;

        void Start()
        {
            mesh = new Mesh();
            GetComponent<MeshFilter>().mesh = mesh;

            vertices = new List<Vector3>();
            triangles = new List<int>();
        }

        public void UpdateCell(int x, int y, Cell newCell)
        {
            // Update the cell in the grid
            grid[x, y] = newCell;

            // Convert the 2D grid coordinates to a 1D index
            int index = y * gridSize.x + x;

            // Add the index to the list of changed cells
            if (!changedCells.Contains(index))
            {
                changedCells.Add(index);
            }

            // Call UpdateMesh in the next frame
            needsUpdate = true;
        }

        private void Update()
        {
            if (needsUpdate)
            {
                UpdateMesh();
                needsUpdate = false;
            }
        }

        private void UpdateMesh()
        {
            // Only update if there are any changes
            if (changedCells.Count > 0)
            {
                // For each changed cell
                foreach (int index in changedCells)
                {
                    // Calculate the x and y coordinates
                    int x = index % gridSize.x;
                    int y = index / gridSize.x;

                    // Recalculate the vertices, triangles and UVs for this cell
                    Vector3 bottomLeft = new Vector3(x * cellSize, y * cellSize);
                    int vertexIndex = vertices.Count;
                    vertices.Add(bottomLeft);
                    vertices.Add(bottomLeft + new Vector3(cellSize, 0));
                    vertices.Add(bottomLeft + new Vector3(cellSize, cellSize));
                    vertices.Add(bottomLeft + new Vector3(0, cellSize));

                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 1);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 3);

                    uvs.Add(new Vector2(x / (float)gridSize.x, y / (float)gridSize.y));
                    uvs.Add(new Vector2((x + 1) / (float)gridSize.x, y / (float)gridSize.y));
                    uvs.Add(new Vector2((x + 1) / (float)gridSize.x, (y + 1) / (float)gridSize.y));
                    uvs.Add(new Vector2(x / (float)gridSize.x, (y + 1) / (float)gridSize.y));
                }

                // Clear the list of changed cells
                changedCells.Clear();

                // Create a new Mesh
                Mesh mesh = new Mesh();
                mesh.name = "Procedural Grid";

                // Assign the vertices, triangles, and UVs
                mesh.vertices = vertices.ToArray();
                mesh.triangles = triangles.ToArray();
                mesh.uv = uvs.ToArray();

                // Recalculate normals to ensure proper shading
                mesh.RecalculateNormals();

                // Assign our mesh to the mesh filter
                meshFilter.mesh = mesh;
            }
        }


    }
}
*/
