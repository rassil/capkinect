// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

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


namespace SkeletalTracking
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class Puzzle : Window
    {
        private Random random;

        private System.Windows.Threading.DispatcherTimer gameTimer;
        private DateTime startTime;
        private DateTime currentTime;

        private System.Windows.Threading.DispatcherTimer selectionTimer;
        private DateTime startSelectionTime;
        private DateTime currentSelectionTime;

        private int loaderFrameIndex = 0;
        private Image[] loader;

        private Image[] logoCap;
        private Image[] logoCapPosition;
        private bool[] imagesState = new bool[7];

        private int imageIndex = -1; // -1 : Aucune image n'est sélectionnée

        private bool newGame;

        private bool loading;
        private bool selectionFinished = false;

        bool closing = false;
        const int skeletonCount = 6;
        Skeleton[] allSkeletons = new Skeleton[skeletonCount];

        private KinectSensor sensor;

        public Puzzle()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            closing = false;

            kinectSensorChooser1.KinectSensorChanged += new DependencyPropertyChangedEventHandler(kinectSensorChooser1_KinectSensorChanged);

            gameTimer = new System.Windows.Threading.DispatcherTimer();
            gameTimer.Tick += new EventHandler(OnTimeEvent);
            gameTimer.Interval = new TimeSpan(10000000);

            selectionTimer = new System.Windows.Threading.DispatcherTimer();
            selectionTimer.Tick += new EventHandler(OnSelectEvent);
            selectionTimer.Interval = new TimeSpan(1000000);

            initGame();
        }

        private void initGame()
        {
            random = new Random();
            newGame = true;
            loader = new Image[] { this.loader0, this.loader1, this.loader2, this.loader3, this.loader4, this.loader5, this.loader6, this.loader7, this.loader8, this.loader9, this.loader10, this.loader11, this.loader12 };
            logoCap = new Image[] { this.liberte, this.audace, this.confiance, this.honnetete, this.plaisir, this.simplicite, this.solidarite };
            logoCapPosition = new Image[] { this.libertePosition, this.audacePosition, this.confiancePosition, this.honnetetePosition, this.plaisirPosition, this.simplicitePosition, this.solidaritePosition };
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            closing = true;
            //StopKinect(kinectSensorChooser1.Kinect);
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
                kinectSensorChooser1.AppConflictOccurred();
            }

            Degree.Content = kinectSensorChooser1.Kinect.ElevationAngle.ToString();
            slider1.Value = kinectSensorChooser1.Kinect.ElevationAngle;
        }

        void GetCameraPoint(Skeleton first, AllFramesReadyEventArgs e)
        {
            using (DepthImageFrame depth = e.OpenDepthImageFrame())
            {
                if (!(depth == null ||
                    kinectSensorChooser1.Kinect == null))
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
                    CameraPosition(Head, headColorPoint);

                    CameraPosition(RightHand, rightColorPoint);
                    moveLoader(rightColorPoint.X, rightColorPoint.Y);

                    CameraPosition(LeftHand, leftColorPoint);
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
            ScalePosition(Head, first.Joints[JointType.Head]);
            ScalePosition(RightHand, first.Joints[JointType.HandRight]);
            ScalePosition(LeftHand, first.Joints[JointType.HandLeft]);

            ScalePosition(canvasLoader, first.Joints[JointType.HandRight]);

            if (continuGame() & !newGame)
            {
                ProcessGesture();
            }
            else
            {
                gameTimer.Stop();
                newGame = true;
            }

            Replay(first.Joints[JointType.HandLeft]);
        }

        private void ProcessGesture()
        {
            int index;

            bool imageHovered = false;

            foreach (Image img in logoCap)
            {
                if (rightHandOnImage(img))
                {
                    imageHovered = true;
                    break;
                }
            }


            // Une image est sélectionnée
            if (imageIndex >= 0)
            {
                stopLoadingAnimation();
                if (goodPosition())
                {
                    freezeIt();
                    imagesState[imageIndex] = true;
                    selectionFinished = false;
                    imageIndex = -1;
                }
                else
                {
                    moveIt();
                }
            }
            // Pas d'image sélectionnée, il faut en sélectionner une
            else
            {
                if (imageHovered)
                {
                    index = getHoveredImageIndex();
                    if (index >= 0)
                    {
                        if (!loading)
                        {
                            startSelectionTime = DateTime.Now;
                            startLoadingAnimation();
                        }
                        selectIt(index);
                    }
                }
                else
                {
                    stopLoadingAnimation();
                }
            }
        }

        void OnTimeEvent(object source, EventArgs e)
        {
            currentTime = DateTime.Now;

            string chronoText = (currentTime - startTime).ToString();
            chronoText = chronoText.Substring(0, 8);

            this.chrono.Content = chronoText;
        }

        void OnSelectEvent(object source, EventArgs e)
        {
            if (loading)
            {
                currentSelectionTime = DateTime.Now;

                if ((currentSelectionTime - startSelectionTime).Seconds >= 0.75f)
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

        private bool continuGame()
        {
            int i = 0;
            bool continu = true;
            while (continu & i < 7)
            {
                continu = imagesState[i];
                i++;
            }
            return !continu;
        }

        private int getHoveredImageIndex()
        {
            if (rightHandOnImage(this.liberte))
            {
                return 0;
            }
            if (rightHandOnImage(this.audace))
            {
                return 1;
            }
            if (rightHandOnImage(this.confiance))
            {
                return 2;
            }
            if (rightHandOnImage(this.honnetete))
            {
                return 3;
            }
            if (rightHandOnImage(this.plaisir))
            {
                return 4;
            }
            if (rightHandOnImage(this.simplicite))
            {
                return 5;
            }
            if (rightHandOnImage(this.solidarite))
            {
                return 6;
            }
            else
            {
                return -1;
            }
        }

        private Image getSelectedImage()
        {
            if (Enumerable.Range(0, 7).Contains(imageIndex))
            {
                return logoCap[imageIndex];
            }
            else
            {
                return null;
            }
        }

        private Image getSelectedImagePosition()
        {
            if (Enumerable.Range(0, 7).Contains(imageIndex))
            {
                return logoCapPosition[imageIndex];
            }
            else
            {
                return null;
            }
        }

        private void showImages()
        {
            foreach (Image img in logoCap)
            {
                img.Visibility = System.Windows.Visibility.Visible;
                unselectImage(img);
            }

            foreach (Image img in logoCapPosition)
            {
                img.Visibility = System.Windows.Visibility.Hidden;
            }
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

        private bool leftHandOnSelectBtn()
        {
            double xImage = Canvas.GetLeft(this.selectBtn) + (this.selectBtn.ActualWidth) / 2;
            double yImage = Canvas.GetTop(this.selectBtn) + (this.selectBtn.ActualHeight) / 2;

            double xHand = Canvas.GetLeft(this.LeftHand) + (this.LeftHand.ActualWidth) / 2;
            double yHand = Canvas.GetTop(this.LeftHand) + (this.LeftHand.ActualHeight) / 2;

            return (Math.Abs(xImage - xHand) <= 50) & (Math.Abs(yImage - yHand) <= 50);
        }

        private bool leftHandOnExitBtn()
        {
            double xImage = Canvas.GetLeft(this.exitBtn) + (this.exitBtn.ActualWidth) / 2;
            double yImage = Canvas.GetTop(this.exitBtn) + (this.exitBtn.ActualHeight) / 2;

            double xHand = Canvas.GetLeft(this.LeftHand) + (this.LeftHand.ActualWidth) / 2;
            double yHand = Canvas.GetTop(this.LeftHand) + (this.LeftHand.ActualHeight) / 2;

            return (Math.Abs(xImage - xHand) <= 50) & (Math.Abs(yImage - yHand) <= 50);
        }

        private bool goodPosition()
        {
            double leftPos = Math.Abs(Canvas.GetLeft(getSelectedImage()) - Canvas.GetLeft(getSelectedImagePosition()));
            double topPos = Math.Abs(Canvas.GetTop(getSelectedImage()) - Canvas.GetTop(getSelectedImagePosition()));
            return (leftPos <= 10 & topPos <= 10);
        }

        private void moveIt()
        {
            double xHand = Canvas.GetLeft(this.RightHand) + (this.RightHand.ActualWidth) / 2;
            double yHand = Canvas.GetTop(this.RightHand) + (this.RightHand.ActualHeight) / 2;
            double newLeft = xHand - (getSelectedImage().ActualWidth) / 2;
            double newTop = yHand - (getSelectedImage().ActualHeight) / 2;
            Canvas.SetLeft(getSelectedImage(), newLeft);
            Canvas.SetTop(getSelectedImage(), newTop);
        }

        private void freezeIt()
        {
            getSelectedImage().Visibility = System.Windows.Visibility.Hidden;
            getSelectedImagePosition().Visibility = System.Windows.Visibility.Visible;
        }

        private void selectIt(int index)
        {
            if (selectionFinished)
            {
                imageIndex = index;
                selectImage(getSelectedImage());
            }
        }

        private void scramble()
        {
            foreach (Image img in logoCap)
            {
                Canvas.SetLeft(img, getRandom(30, 550));
                Canvas.SetTop(img, getRandom(30, 350));
            }

            for (int i = 0; i < 7; i++)
            {
                imagesState[i] = false;
            }

            showImages();

            newGame = false;

            startTime = DateTime.Now;

            gameTimer.Start();
            selectionTimer.Start();
            imageIndex = -1;
        }

        private double getRandom(int min, int max)
        {
            return random.Next(min, max);
        }

        public void selectImage(Image img)
        {
            img.Opacity = 1;
        }

        public void unselectImage(Image img)
        {
            img.Opacity = 0.75;
        }

        private void Replay(Joint handLeft)
        {
            if (leftHandOnSelectBtn())
            {
                this.selectBtn.Fill = Brushes.DarkGreen;
                scramble();
            }
            else
            {
                this.selectBtn.Fill = Brushes.DarkRed;
            }

            if (leftHandOnExitBtn())
            {
                this.Hide();
            }
        }

        private void btnangle_Click(object sender, RoutedEventArgs e)
        {
            if (kinectSensorChooser1.Kinect.ElevationAngle != (int)slider1.Value)
                kinectSensorChooser1.Kinect.ElevationAngle = (int)slider1.Value;
        }

        private void slider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int n = (int)slider1.Value;

            Degree.Content = n.ToString();
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
