using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Contexts;
using Android.Content;
//using Android.Graphics;
using Android.Opengl;
using Android.Telecom;
using Android.Util;
using Android.Views;
using Google.AR.Core;
using Java.Nio;


namespace HelloAR
{
    public class PlaneDivider
    {      
        public static List<HitResult> DividePlaneIntoCells(Frame frame, Plane plane) 
        {
            const float desiredCellSize = 0.1f; // e.g., 1 meter
            List<HitResult> result = new List<HitResult>();
            Pose centerPose = plane.CenterPose;
            Camera camera = frame.Camera;

            //curently using plane as rectangular, obviously not correct, should use polygon
            // Get the dimensions of the plane
            float planeWidth = plane.ExtentX;
            float planeHeight = plane.ExtentZ;

            // Calculate how many cells of the desired size can fit on the plane
            int numCellsX = (int)(planeWidth / desiredCellSize);
            int numCellsZ = (int)(planeHeight / desiredCellSize);

            // The actual size of the cells might need to be adjusted if the plane's dimensions
            // are not a multiple of the desired cell size. This can be done by simply dividing
            // the plane's dimensions by the number of cells.
            float actualCellSizeX = planeWidth / numCellsX;
            float actualCellSizeZ = planeHeight / numCellsZ;

            // Now you can create your cells using these dimensions
            for (int i = 0; i < numCellsX; i++)
            {
                for (int j = 0; j < numCellsZ; j++)
                {
                    // Calculate the center of each cell in the plane's local space
                    float x = i * actualCellSizeX + actualCellSizeX / 2;
                    float z = j * actualCellSizeZ + actualCellSizeZ / 2;

                    // Transform the cell center from the plane's local coordinate system to world space
                    var cellCenterWorld = new float[] {
                        centerPose.Tx() + x * centerPose.GetXAxis()[0] + z * centerPose.GetZAxis()[0],
                        centerPose.Ty() + x * centerPose.GetXAxis()[1] + z * centerPose.GetZAxis()[1],
                        centerPose.Tz() + x * centerPose.GetXAxis()[2] + z * centerPose.GetZAxis()[2]
                    };

                    // Transform from world coordinates to screen coordinates
                    var screenPosition = worldToScreenPoint(new float[] { cellCenterWorld[0], cellCenterWorld[1], cellCenterWorld[2], 1.0f }, camera);

                    // Perform a hit test at the center of the cell.
                    foreach (HitResult hit in frame.HitTest(screenPosition[0], screenPosition[1]))
                {
                    var trackable = hit.Trackable;
                    // Do something with trackable...
                    //if (trackable is Plane){
                        result.Add(hit);
                        break;
                    //}
                     
                    }
                }
            }
            return result;
        }

        // can ofc be optimized by transforming all points at once
        public static float[] worldToScreenPoint(float[] worldPoint, Camera camera)
        {
            DisplayMetrics displayMetrics = new DisplayMetrics();
            int screenWidth = 1080; // displayMetrics.WidthPixels;
            int screenHeight = 2176; //displayMetrics.HeightPixels;

            // Get the projection matrix and view matrix
            float[] projectionMatrix = new float[16];
            camera.GetProjectionMatrix(projectionMatrix, 0, 0.1f, 25.0f);
            float[] viewMatrix = new float[16];
            camera.GetViewMatrix(viewMatrix,0);


            // Combine the projection matrix and view matrix to create the camera matrix
            float[] cameraMatrix = new float[16];
            Matrix.MultiplyMM(cameraMatrix, 0, projectionMatrix, 0, viewMatrix, 0);

            // Transform object's world coordinates to camera coordinates
            float[] objectCameraCoordinates = new float[4];
            Matrix.MultiplyMV(objectCameraCoordinates, 0, cameraMatrix, 0, worldPoint, 0);

            // Apply perspective division to obtain normalized device coordinates (NDC)
            float ndcX = objectCameraCoordinates[0] / objectCameraCoordinates[3];
            float ndcY = objectCameraCoordinates[1] / objectCameraCoordinates[3];

            // Map NDC coordinates to screen coordinates
            float screenX = (ndcX + 1.0f) * 0.5f * screenWidth;
            float screenY = (1.0f - ndcY) * 0.5f * screenHeight;

            return new float[] { screenX, screenY };
        }


    }

}

