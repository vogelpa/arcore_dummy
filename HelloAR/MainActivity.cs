using Android.App;
using Android.Support.V7.App;
using Android.Widget;
using Android.OS;
using Android.Opengl;
using Android.Media;
using Google.AR.Core;
using Android.Util;
using Java.Interop;
using Javax.Microedition.Khronos.Opengles;
using Android.Support.Design.Widget;
using System.Collections.Generic;
using Android.Views;
using Android.Support.V4.Content;
using Android.Support.V4.App;
using Javax.Microedition.Khronos.Egl;
using System.Collections.Concurrent;
using System;
using Google.AR.Core.Exceptions;
using Java.Util.Concurrent;
using Org.W3c.Dom;

namespace HelloAR
{
    [Activity(Label = "Hello AR", MainLauncher = true, Icon = "@mipmap/icon", Theme = "@style/Theme.AppCompat.NoActionBar", ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize, ScreenOrientation = Android.Content.PM.ScreenOrientation.Locked)]
    public class MainActivity : AppCompatActivity, GLSurfaceView.IRenderer, Android.Views.View.IOnTouchListener
    {
        const string TAG = "HELLO-AR";

        // Rendering. The Renderers are created here, and initialized when the GL surface is created.
        GLSurfaceView mSurfaceView;

        Session mSession;
        BackgroundRenderer mBackgroundRenderer = new BackgroundRenderer();
        GestureDetector mGestureDetector;
        Snackbar mLoadingMessageSnackbar = null;
        DisplayRotationHelper mDisplayRotationHelper;

        ObjectRenderer mVirtualImage = new ObjectRenderer();
        PlaneRenderer mPlaneRenderer = new PlaneRenderer();
        PointCloudRenderer mPointCloud = new PointCloudRenderer();
        // Temporary matrix allocated here to reduce number of allocations for each frame.
        static float[] mAnchorMatrix = new float[16];

        ConcurrentQueue<MotionEvent> mQueuedSingleTaps = new ConcurrentQueue<MotionEvent>();

        // Tap handling and UI.
        List<Anchor> mImageAnchors = new List<Anchor>();

        List<Image> mImages = new List<Image>();


        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.Main);
            mSurfaceView = FindViewById<GLSurfaceView>(Resource.Id.surfaceview);
            mDisplayRotationHelper = new DisplayRotationHelper(this);

            Java.Lang.Exception exception = null;
            string message = null;

            try
            {
                mSession = new Session(/*context=*/this);
            }
            catch (UnavailableArcoreNotInstalledException e)
            {
                message = "Please install ARCore";
                exception = e;
            }
            catch (UnavailableApkTooOldException e)
            {
                message = "Please update ARCore";
                exception = e;
            }
            catch (UnavailableSdkTooOldException e)
            {
                message = "Please update this app";
                exception = e;
            }
            catch (Java.Lang.Exception e)
            {
                exception = e;
                message = "This device does not support AR";
            }

            if (message != null)
            {
                Toast.MakeText(this, message, ToastLength.Long).Show();
                return;
            }

            // Create default config, check is supported, create session from that config.
            var config = new Google.AR.Core.Config(mSession);
            if (!mSession.IsSupported(config))
            {
                Toast.MakeText(this, "This device does not support AR", ToastLength.Long).Show();
                Finish();
                return;
            }
            mSession.Configure(config);

            mGestureDetector = new Android.Views.GestureDetector(this, new SimpleTapGestureDetector
            {
                SingleTapUpHandler = (MotionEvent arg) =>
                {
                    onSingleTap(arg);
                    return true;
                },
                DownHandler = (MotionEvent arg) => true
            });

            mSurfaceView.SetOnTouchListener(this);

            // Set up renderer.
            mSurfaceView.PreserveEGLContextOnPause = true;
            mSurfaceView.SetEGLContextClientVersion(2);
            mSurfaceView.SetEGLConfigChooser(8, 8, 8, 8, 16, 0); // Alpha used for plane blending.
            mSurfaceView.SetRenderer(this);
            mSurfaceView.RenderMode = Rendermode.Continuously;
        }


        protected override void OnResume()
        {
            base.OnResume();

            // ARCore requires camera permissions to operate. If we did not yet obtain runtime
            // permission on Android M and above, now is a good time to ask the user for it.
            if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.Camera) == Android.Content.PM.Permission.Granted)
            {
                if (mSession != null)
                {
                    showLoadingMessage();
                    // Note that order matters - see the note in onPause(), the reverse applies here.
                    mSession.Resume();
                }

                // the app may crash here because of a race condition if you've not YET accepted camera 
                // permissions. just accept the permissions, and then when the app crashes, restart it,
                // and it should be fine. 
                mSurfaceView.OnResume();
                mDisplayRotationHelper.OnResume();
            }
            else
            {
                ActivityCompat.RequestPermissions(this, new string[] { Android.Manifest.Permission.Camera }, 0);
            }
        }

        protected override void OnPause()
        {
            base.OnPause();
            if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.Camera) == Android.Content.PM.Permission.Granted)
            {
                // Note that the order matters - GLSurfaceView is paused first so that it does not try
                // to query the session. If Session is paused before GLSurfaceView, GLSurfaceView may
                // still call mSession.update() and get a SessionPausedException.
                mDisplayRotationHelper.OnPause();
                mSurfaceView.OnPause();
                if (mSession != null)
                    mSession.Pause();
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Android.Content.PM.Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.Camera) != Android.Content.PM.Permission.Granted)
            {
                Toast.MakeText(this, "Camera permission is needed to run this application", ToastLength.Long).Show();
                Finish();
            }
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);

            if (hasFocus)
            {
                // Standard Android full-screen functionality.
                //Window.DecorView.SystemUiVisibility = Android.Views.SystemUiFlags.LayoutStable
                //| Android.Views.SystemUiFlags.LayoutHideNavigation
                //| Android.Views.SystemUiFlags.LayoutFullscreen
                //| Android.Views.SystemUiFlags.HideNavigation
                //| Android.Views.SystemUiFlags.Fullscreen
                //| Android.Views.SystemUiFlags.ImmersiveSticky;

                Window.AddFlags(WindowManagerFlags.KeepScreenOn);
            }
        }

        private void onSingleTap(MotionEvent e)
        {
            // Queue tap if there is space. Tap is lost if queue is full.
            if (mQueuedSingleTaps.Count < 16)
                mQueuedSingleTaps.Enqueue(e);
        }


        public void OnSurfaceCreated(IGL10 gl, Javax.Microedition.Khronos.Egl.EGLConfig config)
        {
            GLES20.GlClearColor(0.1f, 0.1f, 0.1f, 1.0f);

            // Create the texture and pass it to ARCore session to be filled during update().
            mBackgroundRenderer.CreateOnGlThread(/*context=*/this);
            if (mSession != null)
                mSession.SetCameraTextureName(mBackgroundRenderer.TextureId);

            // Prepare the other rendering objects.
            try
            {
               
                mVirtualImage.CreateOnGlThread(/*context=*/this, "2D.obj", "andy.png");
                //mVirtualImage.setMaterialProperties(0.0f, 3.5f, 1.0f, 6.0f);

            }
            catch (Java.IO.IOException e)
            {
                Log.Error(TAG, "Failed to read obj file");
            }

            try
            {
                mPlaneRenderer.CreateOnGlThread(/*context=*/this, "trigrid.png");
            }
            catch (Java.IO.IOException e)
            {
                Log.Error(TAG, "Failed to read plane texture");
            }
            mPointCloud.CreateOnGlThread(/*context=*/this);
        }

        public void OnSurfaceChanged(IGL10 gl, int width, int height)
        {
            mDisplayRotationHelper.OnSurfaceChanged(width, height);
            GLES20.GlViewport(0, 0, width, height);
        }

        public void OnDrawFrame(IGL10 gl)
        {
            // Clear screen to notify driver it should not load any pixels from previous frame.
            GLES20.GlClear(GLES20.GlColorBufferBit | GLES20.GlDepthBufferBit);

            if (mSession == null)
                return;

            // Notify ARCore session that the view size changed so that the perspective matrix and the video background
            // can be properly adjusted
            mDisplayRotationHelper.UpdateSessionIfNeeded(mSession);

            try
            {
                // Obtain the current frame from ARSession. When the configuration is set to
                // UpdateMode.BLOCKING (it is by default), this will throttle the rendering to the
                // camera framerate.
                Frame frame = mSession.Update();
                Camera camera = frame.Camera;

                // Handle taps. Handling only one tap per frame, as taps are usually low frequency
                // compared to frame rate.
                MotionEvent tap = null;
                mQueuedSingleTaps.TryDequeue(out tap);

                // Add a floating image if we tap and are in tracking state
                if (tap != null && camera.TrackingState == TrackingState.Tracking)
                {
                    floatingPictures(frame);                    
                }  

                // Draw background.
                mBackgroundRenderer.Draw(frame);

                // If not tracking, don't draw 3d objects.
                if (camera.TrackingState == TrackingState.Paused)
                    return;

                // Get projection matrix.
                float[] projmtx = new float[16];
                camera.GetProjectionMatrix(projmtx, 0, 0.1f, 100.0f);

                // Get camera matrix and draw.
                float[] viewmtx = new float[16];
                camera.GetViewMatrix(viewmtx, 0);

                // Compute lighting from average intensity of the image.
                var lightIntensity = frame.LightEstimate.PixelIntensity;

                // Visualize tracked points.
                var pointCloud = frame.AcquirePointCloud();
                mPointCloud.Update(pointCloud);
                mPointCloud.Draw(camera.DisplayOrientedPose, viewmtx, projmtx);

                // App is repsonsible for releasing point cloud resources after using it
                pointCloud.Release();

                var planes = new List<Plane>();
                foreach (var p in mSession.GetAllTrackables(Java.Lang.Class.FromType(typeof(Plane))))
                {
                    var plane = (Plane)p;
                    planes.Add(plane);
                }

                // Check if we detected at least one plane. If so, hide the loading message.
                if (mLoadingMessageSnackbar != null)
                {
                    foreach (var plane in planes)
                    {
                        if (plane.GetType() == Plane.Type.HorizontalUpwardFacing
                                && plane.TrackingState == TrackingState.Tracking)
                        {
                            hideLoadingMessage();
                            break;
                        }
                    }
                }

                // Visualize planes.
                mPlaneRenderer.DrawPlanes(planes, camera.DisplayOrientedPose, projmtx);

                // Visualize anchors created by touch.
                float scaleFactor =  0.005f;
                foreach (var imageAnchor in mImageAnchors)
                {
                    if (imageAnchor.TrackingState != TrackingState.Tracking)
                        continue;

                    // Get the current combined pose of an Anchor and Plane in world space. The Anchor
                    // and Plane poses are updated during calls to session.update() as ARCore refines
                    // its estimate of the world.
                    imageAnchor.Pose.ToMatrix(mAnchorMatrix, 0);
                    // Update and draw the model and its shadow.
                    mVirtualImage.updateModelMatrix(mAnchorMatrix, scaleFactor);
                    mVirtualImage.Draw(viewmtx, projmtx, lightIntensity);
                }
            }

            catch (System.Exception ex)
            {
                // Avoid crashing the application due to unhandled exceptions.
                Log.Error(TAG, "Exception on the OpenGL thread", ex);
            }
        }

        private void floatingPictures(Frame frame)
        {
            // Capture the camera image.
            Camera camera = frame.Camera;
            //Image image = frame.AcquireCameraImage();

            Pose pose = camera.DisplayOrientedPose;

            float[] translation = pose.GetTranslation();
            translation[2] = 0.5f; // Assuming Z-axis is up.

            // Create the adjusted pose.
            var adjustedPose = new Pose(translation, pose.GetRotationQuaternion());
            
            // Create a quad to display the image.
            Anchor anchor = mSession.CreateAnchor(pose);
    
            // Anchor the quad in the world.
            mImageAnchors.Add(anchor);
            //mImages.Add(image);
        }
        private void showLoadingMessage()
        {
            this.RunOnUiThread(() =>
            {
                ImageView image = new ImageView(this);
                image.SetImageResource(Resource.Drawable.hand_holding_phone);
                image.SetAdjustViewBounds(true);
                image.SetScaleType(ImageView.ScaleType.FitCenter);
                mLoadingMessageSnackbar = Snackbar.Make(FindViewById(Android.Resource.Id.Content),
                    "Searching for surfaces...", Snackbar.LengthIndefinite);
                Snackbar.SnackbarLayout layout = (Snackbar.SnackbarLayout)mLoadingMessageSnackbar.View;
                layout.SetMinimumHeight(100);
                layout.SetBackgroundColor(Android.Graphics.Color.Transparent); // set background to be transparent
                layout.Alpha = 1.0f;

                // set background color of the text view to gray
                TextView textView = layout.FindViewById<TextView>(Resource.Id.snackbar_text);
                textView.SetBackgroundColor(Android.Graphics.Color.DarkGray);

                layout.AddView(image, 0);
                mLoadingMessageSnackbar.Show();
            });
        }

        private void hideLoadingMessage()
        {
            this.RunOnUiThread(() =>
            {
                mLoadingMessageSnackbar.Dismiss();
                mLoadingMessageSnackbar = null;
            });

        }

        public bool OnTouch(View v, MotionEvent e)
        {
            return mGestureDetector.OnTouchEvent(e);
        }
    }

    class SimpleTapGestureDetector : GestureDetector.SimpleOnGestureListener
    {
        public Func<MotionEvent, bool> SingleTapUpHandler { get; set; }

        public override bool OnSingleTapUp(MotionEvent e)
        {
            return SingleTapUpHandler?.Invoke(e) ?? false;
        }

        public Func<MotionEvent, bool> DownHandler { get; set; }

        public override bool OnDown(MotionEvent e)
        {
            return DownHandler?.Invoke(e) ?? false;
        }
    }
}
