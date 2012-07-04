using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Coding4Fun.Kinect.Wpf;
using Coding4Fun.Kinect;
using System.IO;
using System.Timers;
using Microsoft.Samples.Kinect.Slideshow;

namespace SkeletalTracking
{
    /// <summary>
    /// Logique d'interaction pour Window1.xaml
    /// </summary>
    public partial class WindowPrincipal : Window
    {
        public WindowPrincipal()
        {
            InitializeComponent();
        }

        private System.Windows.Threading.DispatcherTimer selectionTimer;
        private DateTime startSelectionTime;
        private DateTime currentSelectionTime;

        private int loaderFrameIndex = 0;
        private Image[] loader;

        private bool loading = false;
        private bool selectionFinished = false;

        bool closing = false;
        Skeleton[] allSkeletons = new Skeleton[6];

        private KinectSensor sensor;


        Puzzle mw;
        MainWindow slide;


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            kinectSensorChooserPrincipal.KinectSensorChanged += new DependencyPropertyChangedEventHandler(kinectSensorChooser1_KinectSensorChanged);

            selectionTimer = new System.Windows.Threading.DispatcherTimer();
            selectionTimer.Tick += new EventHandler(OnSelectEvent);
            selectionTimer.Interval = new TimeSpan(1000000);
            selectionTimer.Start();

            loader = new Image[] { this.loader0, this.loader1, this.loader2, this.loader3, this.loader4, this.loader5, this.loader6, this.loader7, this.loader8, this.loader9, this.loader10, this.loader11, this.loader12 };

            mw = new Puzzle();
            slide = new MainWindow();
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            closing = true;
            StopKinect(kinectSensorChooserPrincipal.Kinect);
            mw.Close();
            slide.Close();
        }

        void kinectSensorChooser1_KinectSensorChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            KinectSensor old = (KinectSensor)e.OldValue;

            StopKinect(old);

            sensor = (KinectSensor)e.NewValue;

            if (sensor == null)
            {
                return;
            }

            var parameters = new TransformSmoothParameters
            {
                Smoothing = 0.7f,
                Correction = 0.3f,
                Prediction = 0.4f,
                JitterRadius = 1.0f,
                MaxDeviationRadius = 0.5f
            };
            sensor.SkeletonStream.Enable(parameters);

            sensor.SkeletonStream.Enable();

            sensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(sensor_AllFramesReady);
            sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

            try
            {
                sensor.Start();
            }
            catch (System.IO.IOException)
            {
                kinectSensorChooserPrincipal.AppConflictOccurred();
            }
        }

        void GetCameraPoint(Skeleton first, AllFramesReadyEventArgs e)
        {
            using (DepthImageFrame depth = e.OpenDepthImageFrame())
            {
                if (!(depth == null ||
                    kinectSensorChooserPrincipal.Kinect == null))
                {
                    //Map a joint location to a point on the depth map
                    //head
                    DepthImagePoint headDepthPoint =
                        depth.MapFromSkeletonPoint(first.Joints[JointType.Head].Position);
                    //right hand
                    DepthImagePoint rightDepthPoint =
                        depth.MapFromSkeletonPoint(first.Joints[JointType.HandRight].Position);
                    //left hand
                    DepthImagePoint leftDepthPoint =
                        depth.MapFromSkeletonPoint(first.Joints[JointType.HandLeft].Position);


                    //Map a depth point to a point on the color image
                    //head
                    ColorImagePoint headColorPoint =
                        depth.MapToColorImagePoint(headDepthPoint.X, headDepthPoint.Y,
                        ColorImageFormat.RgbResolution640x480Fps30);
                    //right hand
                    ColorImagePoint rightColorPoint =
                        depth.MapToColorImagePoint(rightDepthPoint.X, rightDepthPoint.Y,
                        ColorImageFormat.RgbResolution640x480Fps30);
                    //left hand
                    ColorImagePoint leftColorPoint =
                        depth.MapToColorImagePoint(leftDepthPoint.X, leftDepthPoint.Y,
                        ColorImageFormat.RgbResolution640x480Fps30);


                    //Set location

                    CameraPosition(RightHand, rightColorPoint);
                    moveLoader(rightColorPoint.X, rightColorPoint.Y);
                }
            }
        }

        Skeleton GetFirstSkeleton(AllFramesReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrameData = e.OpenSkeletonFrame())
            {
                if (skeletonFrameData == null)
                {
                    return null;
                }

                skeletonFrameData.CopySkeletonDataTo(allSkeletons);

                //get the first tracked skeleton
                Skeleton first = (from s in allSkeletons
                                  where s.TrackingState == SkeletonTrackingState.Tracked
                                  select s).FirstOrDefault();

                return first;
            }
        }

        private void CameraPosition(FrameworkElement element, ColorImagePoint point)
        {
            //Divide by 2 for width and height so point is right in the middle 
            // instead of in top/left corner
            Canvas.SetLeft(element, point.X - element.Width / 2);
            Canvas.SetTop(element, point.Y - element.Height / 2);
        }

        private void ScalePosition(FrameworkElement element, Joint joint)
        {
            //convert the value to X/Y
            //Joint scaledJoint = joint.ScaleTo(1000, 500);

            //convert & scale (.3 = means 1/3 of joint distance)
            Joint scaledJoint = joint.ScaleTo(990, 600, .3f, .3f);

            Canvas.SetLeft(element, scaledJoint.Position.X);
            Canvas.SetTop(element, scaledJoint.Position.Y);
        }

        void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            if (closing)
            {
                return;
            }

            //Get a skeleton
            Skeleton first = GetFirstSkeleton(e);

            if (first == null)
            {
                return;
            }

            GetCameraPoint(first, e);

            //set scaled position
            ScalePosition(RightHand, first.Joints[JointType.HandRight]);

            ScalePosition(canvasLoader, first.Joints[JointType.HandRight]);

            ProcessGesture();
        }

        private void ProcessGesture()
        {
            if (!mw.IsVisible & !slide.IsVisible)
            {
                if (rightHandOnImage(this.image1))
                {
                    if (!loading)
                    {
                        startSelectionTime = DateTime.Now;
                        startLoadingAnimation();
                    }
                    if (selectionFinished)
                    {
                        //selectionTimer.Stop();
                        mw.Close();
                        mw = new Puzzle();
                        mw.Show();
                        selectionFinished = false;
                    }
                }
                else
                {
                    if (rightHandOnImage(this.image2))
                    {
                        if (!loading)
                        {
                            startSelectionTime = DateTime.Now;
                            startLoadingAnimation();
                        }
                        if (selectionFinished)
                        {
                            //selectionTimer.Stop();
                            slide.Close();
                            slide = new MainWindow();
                            slide.Show();
                            selectionFinished = false;
                        }
                    }
                    else
                    {
                        stopLoadingAnimation();
                    }
                }
            }
        }
        void OnSelectEvent(object source, EventArgs e)
        {
            if (loading)
            {
                currentSelectionTime = DateTime.Now;

                if ((currentSelectionTime - startSelectionTime).Seconds >= 1)
                {
                    selectionFinished = true;
                }
                AnimateLoader(loaderFrameIndex);
                loaderFrameIndex = (loaderFrameIndex + 1) % 13;
            }
        }

        void startLoadingAnimation()
        {
            loading = true;
            selectionFinished = false;
            loaderFrameIndex = 0;
        }

        void stopLoadingAnimation()
        {
            loading = false;
            selectionFinished = true;
            foreach (Image img in loader)
            {
                img.Visibility = System.Windows.Visibility.Hidden;
            }
        }

        void AnimateLoader(int frameIndex)
        {
            foreach (Image img in loader)
            {
                img.Visibility = System.Windows.Visibility.Hidden;
            }

            loader[frameIndex].Visibility = System.Windows.Visibility.Visible;
        }

        void moveLoader(double left, double top)
        {
            Canvas.SetLeft(canvasLoader, left);
            Canvas.SetTop(canvasLoader, top);
        }

        
        private bool rightHandOnImage(Image img)
        {
            if (img.Visibility.Equals(Visibility.Visible))
            {
                double xImage = Canvas.GetLeft(img) + (img.ActualWidth) / 2;
                double yImage = Canvas.GetTop(img) + (img.ActualHeight) / 2;

                double xHand = Canvas.GetLeft(this.RightHand) + (this.RightHand.ActualWidth) / 2;
                double yHand = Canvas.GetTop(this.RightHand) + (this.RightHand.ActualHeight) / 2;

                return (Math.Abs(xImage - xHand) <= 50) & (Math.Abs(yImage - yHand) <= 50);
            }
            else
                return false;
        }

        private void StopKinect(KinectSensor sensor)
        {
            if (sensor != null)
            {
                if (sensor.IsRunning)
                {
                    //stop sensor 
                    sensor.Stop();

                    //stop audio if not null
                    if (sensor.AudioSource != null)
                    {
                        sensor.AudioSource.Stop();
                    }
                }
            }
        }
    }
}

