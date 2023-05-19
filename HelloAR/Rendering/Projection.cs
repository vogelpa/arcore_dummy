using System;
using System.Linq;
using System.Collections.Generic;
using Android.Content;
//using Android.Hardware;
//using Android.Graphics;
using Android.Opengl;
using Android.Renderscripts;
using Android.Telecom;
using Google.AR.Core;
using Java.Nio;
using TriangleNet;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using Android.Graphics.Drawables;

namespace HelloAR
{
    public class Projection
    {

        public static void GetMeshFromFrame(Frame frame)
        {
            //Mesh mesh;
            // Acquire the point cloud from ARFrame
            PointCloud pointCloud = frame.AcquirePointCloud();

            // Get the camera from the frame
            Camera camera = frame.Camera;

            float[] projectionMatrix = new float[16];
            camera.GetProjectionMatrix(projectionMatrix, 0, 0.1f, 25.0f);

            float[] viewMatrix = new float[16];
            camera.GetViewMatrix(viewMatrix, 0);

            float[] projectionViewMatrix = new float[16];
            Matrix.MultiplyMM(projectionViewMatrix, 0, projectionMatrix, 0, viewMatrix, 0);

            // Get the camera pose
            Pose cameraPose = camera.Pose;

            // Get the points of the point cloud
            FloatBuffer pointsBuffer = pointCloud.Points;


            // Determine the number of points in the buffer
            int numPoints = pointsBuffer.Remaining() / 4; // Assuming each point has 4 float values (X, Y, Z, Confidence)

            float[] gridVertices = new float[numPoints*3];

            // Create an array to store the points
            float[] screenCoordinates = new float[numPoints*2];

            // Extract the points from the buffer
            for (int i = 0; i < numPoints; i++)
            {
                float x = pointsBuffer.Get();
                float y = pointsBuffer.Get();
                float z = pointsBuffer.Get();
                
                // Skip confidence value, but still advance the buffer
                pointsBuffer.Get();

                gridVertices[3*i]   = x;
                gridVertices[3*i+1] = y;
                gridVertices[3*i+2] = z;

                // Create a 4x1 homogeneous point vector [x, y, z, 1]
                float[] pointVector = { x, y, z, 1.0f };

                // Apply projection matrix to obtain screen coordinates
                float[] projectedPoint = new float[4];
                Matrix.MultiplyMV(projectedPoint, 0, projectionViewMatrix, 0, pointVector, 0);

                // Normalize the screen coordinates
                projectedPoint[0] /= projectedPoint[3];
                projectedPoint[1] /= projectedPoint[3];

                // Store the screen coordinates in the array
                screenCoordinates[i * 2] = projectedPoint[0];
                screenCoordinates[i * 2 + 1] = projectedPoint[1];
            }
            // Assuming that 'screenCoordinates' is your 2D points array
            var polygon = new Polygon();
            for (int i = 0; i<screenCoordinates.Length; i += 2)
            {
                polygon.Add(new Vertex(screenCoordinates[i], screenCoordinates[i + 1]));
            }

            // Create the mesh
            var mesh = (TriangleNet.Mesh)polygon.Triangulate(new ConstraintOptions() { ConformingDelaunay = false, SegmentSplitting = 1 });

            // Convert to your desired format, if necessary
            //var triangles = mesh.Triangles.Select(t => (t.GetVertex(0), t.GetVertex(1), t.GetVertex(2))).ToArray();

            // Get the grid segments from the mesh
            IEnumerable<TriangleNet.Topology.SubSegment> segments = mesh.Segments;

            // Flatten the segments into an int array
            List<int> gridSegmentsArray = segments.SelectMany(s => new int[] { s.P0, s.P1 }).ToList();

            // Get all triangles in the mesh
            IEnumerable<TriangleNet.Topology.Triangle> triangles = mesh.Triangles;
            // Create a list to store the segments
      
            // Iterate through each triangle and render them
            foreach (var triangle in triangles)
            {
                gridSegmentsArray.AddRange(new int[] { 
                    triangle.GetVertexID(0), triangle.GetVertexID(1), 
                    triangle.GetVertexID(0), triangle.GetVertexID(2), 
                    triangle.GetVertexID(1), triangle.GetVertexID(2) });
            }
            int[] gridSegments = gridSegmentsArray.ToArray();


            // Create a ByteBuffer for the grid vertices
            ByteBuffer vertexByteBuffer = ByteBuffer.AllocateDirect(gridVertices.Length * 4); // Multiply by 4 because floats have 4 bytes
            vertexByteBuffer.Order(ByteOrder.NativeOrder());

            // Convert it to a FloatBuffer
            FloatBuffer vertexBuffer = vertexByteBuffer.AsFloatBuffer();
            vertexBuffer.Put(gridVertices);
            vertexBuffer.Position(0);

            // Create a ByteBuffer for the grid segments
            ByteBuffer segmentByteBuffer = ByteBuffer.AllocateDirect(gridSegments.Length * 4); // Multiply by 4 because ints have 4 bytes
            segmentByteBuffer.Order(ByteOrder.NativeOrder());

            // Convert it to an IntBuffer
            IntBuffer segmentBuffer = segmentByteBuffer.AsIntBuffer();
            segmentBuffer.Put(gridSegments);
            segmentBuffer.Position(0);

            // Enable the attribute at location 0 (assuming that's where your vertex position is)
            GLES20.GlEnableVertexAttribArray(0);

            // Pass the vertex buffer to the GPU
            GLES20.GlVertexAttribPointer(0, 2, GLES20.GlFloat, false, 0, vertexBuffer);

            // Create a simple shader program
            int vertexShader = GLES20.GlCreateShader(GLES20.GlVertexShader);
            // ... Compile and set vertex shader source

            int fragmentShader = GLES20.GlCreateShader(GLES20.GlFragmentShader);
            // ... Compile and set fragment shader source

            int shaderProgram = GLES20.GlCreateProgram();
            GLES20.GlAttachShader(shaderProgram, vertexShader);
            GLES20.GlAttachShader(shaderProgram, fragmentShader);
            GLES20.GlLinkProgram(shaderProgram);

            // Use the shader program to draw the grid segments
            GLES20.GlUseProgram(shaderProgram);

            // Set uniform color for the grid
            int colorUniformLocation = GLES20.GlGetUniformLocation(shaderProgram, "uColor");
            GLES20.GlUniform4f(colorUniformLocation, 1.0f, 1.0f, 1.0f, 1.0f); // Set white color (RGBA)

            GLES20.GlLineWidth(5.0f); // Set the line width before drawing

            // Draw the grid segments
            GLES20.GlDrawElements(GLES20.GlLines, gridSegments.Length, GLES20.GlUnsignedInt, segmentBuffer);

            // Disable the attribute after drawing
            GLES20.GlDisableVertexAttribArray(0);

        }
    }
}

