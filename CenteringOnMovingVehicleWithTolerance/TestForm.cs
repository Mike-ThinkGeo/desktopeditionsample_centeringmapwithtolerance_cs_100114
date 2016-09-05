using System;
using System.Windows.Forms;
using System.Collections.ObjectModel;
using ThinkGeo.MapSuite.Core;
using System.IO;
using System.Data;
using ThinkGeo.MapSuite.DesktopEdition;


namespace  CenteringOnMovingVehicleWithTolerance
{
    public partial class TestForm : Form
    {
        private StreamReader mGPSData = new StreamReader(@"..\..\data\GPSinfo.txt");
        private Timer timer;
        private double previousLong;
        private double previousLat;

        public TestForm()
        {
            InitializeComponent();
            timer = new Timer();
        }

        private void TestForm_Load(object sender, EventArgs e)
        {
            //Sets timers properties.
            timer.Interval = 800; 
            timer.Tick += new EventHandler(timer_Tick);


            winformsMap1.MapUnit = GeographyUnit.DecimalDegree;
            winformsMap1.CurrentExtent = new RectangleShape(-97.7591, 30.3126, -97.7317, 30.2964);
            winformsMap1.BackgroundOverlay.BackgroundBrush = new GeoSolidBrush(GeoColor.FromArgb(255, 198, 255, 255));

            //Displays the World Map Kit as a background.
            ThinkGeo.MapSuite.DesktopEdition.WorldMapKitWmsDesktopOverlay worldMapKitDesktopOverlay = new ThinkGeo.MapSuite.DesktopEdition.WorldMapKitWmsDesktopOverlay();
            winformsMap1.Overlays.Add(worldMapKitDesktopOverlay);

            //InMemoryFeatureLayer for vehicle.
            InMemoryFeatureLayer carLayer = new InMemoryFeatureLayer();
            carLayer.ZoomLevelSet.ZoomLevel01.DefaultPointStyle.PointType = PointType.Bitmap;
            carLayer.ZoomLevelSet.ZoomLevel01.DefaultPointStyle.Image = new GeoImage(@"..\..\data\sedan.png");
            carLayer.ZoomLevelSet.ZoomLevel01.DefaultPointStyle.RotationAngle = 45;
            carLayer.ZoomLevelSet.ZoomLevel01.ApplyUntilZoomLevel = ApplyUntilZoomLevel.Level20;
            carLayer.InternalFeatures.Add("Car", new Feature(new PointShape()));

            LayerOverlay vehicleOverlay = new LayerOverlay();
            vehicleOverlay.Layers.Add("CarLayer", carLayer);
            winformsMap1.Overlays.Add("VehicleOverlay",vehicleOverlay);

            //InMemoryFeatureLayer for Tolerance RectangleShape. (Displayed for the purpose of the project. It does not need to be displayed for a real world application)
            InMemoryFeatureLayer toleranceLayer = new InMemoryFeatureLayer();
            toleranceLayer.ZoomLevelSet.ZoomLevel01.DefaultAreaStyle = AreaStyles.CreateSimpleAreaStyle(GeoColor.StandardColors.Transparent, GeoColor.StandardColors.Green, 2);
            toleranceLayer.ZoomLevelSet.ZoomLevel01.ApplyUntilZoomLevel = ApplyUntilZoomLevel.Level20;

            //Uses a Rectangle 60% smaller than the current extent of the map for the tolerance.
            RectangleShape toleranceRectangleShape = new RectangleShape(winformsMap1.CurrentExtent.UpperLeftPoint, winformsMap1.CurrentExtent.LowerRightPoint);
            toleranceRectangleShape.ScaleDown(60);

            toleranceLayer.InternalFeatures.Add("Tolerance", new Feature(toleranceRectangleShape));

            LayerOverlay toleranceOverlay = new LayerOverlay();
            toleranceOverlay.Layers.Add("ToleranceLayer", toleranceLayer);
            winformsMap1.Overlays.Add("ToleranceOverlay",toleranceOverlay);


            winformsMap1.Refresh();

            timer.Start();
       }


        void timer_Tick(object sender, EventArgs e)
        {
            //Gets the GPS info from the textfile.
            DataTable carData = GetCarData();

            double angle;
            LayerOverlay vehicleOverlay = (LayerOverlay)winformsMap1.Overlays["VehicleOverlay"];
            InMemoryFeatureLayer carLayer = vehicleOverlay.Layers["CarLayer"] as InMemoryFeatureLayer;

            PointShape pointShape = carLayer.InternalFeatures[0].GetShape() as PointShape;

            // Get the Row of Data we are working with.
            DataRow carDataRow = carData.Rows[0];

            double Lat = Convert.ToDouble(carDataRow["LAT"]);
            double Long = Convert.ToDouble(carDataRow["LONG"]);

            if (previousLong == 0)
            {
                previousLong = Long;
                previousLat = Lat;
            }

            double Xdiff = previousLong - Long;
            double Ydiff = previousLat - Lat;

            //Gets the angle based on the current GPS position and the previous one to get the direction of the vehicle.
            angle = GetAngleFromTwoVertices(new Vertex(previousLong, previousLat), new Vertex(Long, Lat));

            carLayer.ZoomLevelSet.ZoomLevel01.DefaultPointStyle.RotationAngle = 90 - (float)angle;

            pointShape.X = Long;
            pointShape.Y = Lat;
            pointShape.Id = "Car";

            carLayer.Open();
            carLayer.EditTools.BeginTransaction();
            carLayer.EditTools.Update(pointShape);
            carLayer.EditTools.CommitTransaction();
            carLayer.Close();

            previousLong = Long;
            previousLat = Lat;

            //Function to center the map to a point (the moving moving vehicle feature)if it goes outside the tolerance.
            RectangleShape toleranceRectangleShape = new RectangleShape(winformsMap1.CurrentExtent.UpperLeftPoint, winformsMap1.CurrentExtent.LowerRightPoint);
            toleranceRectangleShape.ScaleDown(60);
            if (toleranceRectangleShape.Contains(new PointShape(Long, Lat)) == false)
            {
                winformsMap1.CenterAt(new PointShape(Long, Lat));

                //Resets the RectangleShape of the tolerance layer (for displaying only)
                LayerOverlay toleranceOverlay = (LayerOverlay)winformsMap1.Overlays["ToleranceOverlay"]; 
                InMemoryFeatureLayer toleranceLayer = toleranceOverlay.Layers["ToleranceLayer"] as InMemoryFeatureLayer;

                RectangleShape newToleranceRectangleShape = new RectangleShape(winformsMap1.CurrentExtent.UpperLeftPoint, winformsMap1.CurrentExtent.LowerRightPoint);
                newToleranceRectangleShape.ScaleDown(60);
                newToleranceRectangleShape.Id = "Tolerance";

                toleranceLayer.Open();
                toleranceLayer.EditTools.BeginTransaction();
                toleranceLayer.EditTools.Update(newToleranceRectangleShape);
                toleranceLayer.EditTools.CommitTransaction();
                toleranceLayer.Close();

                winformsMap1.Refresh();
            }
            else
            {
                winformsMap1.Refresh(vehicleOverlay);
            }

        }

        private DataTable GetCarData()
        {
            DataTable datatable = new DataTable();
            datatable.Columns.Add("LAT");
            datatable.Columns.Add("LONG");

            string strLattitude = "";
            string strLongitude = "";

            // Read the next line from the text file with GPS data in it.
            string strCurrentText = mGPSData.ReadLine();

            if (strCurrentText == "")
            {
                mGPSData.BaseStream.Seek(0, SeekOrigin.Begin);
                strCurrentText = mGPSData.ReadLine();
            }

            while (strCurrentText != null)
            {
                // Every other line is a "/" and we want to skip those.
                if (strCurrentText.Trim() != "/")
                {
                    string[] strSplit = strCurrentText.Split(','); // (':');
                    strLongitude = strSplit[0];
                    strLattitude = strSplit[1];
                    break;
                }
                strCurrentText = mGPSData.ReadLine();
            }

            object[] objs = new object[2] { strLattitude, strLongitude };

            datatable.Rows.Add(objs);

            return datatable;
        }

        //We assume that the angle is based on a third point that is on top of b on the same x axis.
        private double GetAngleFromTwoVertices(Vertex b, Vertex c)
        {
            double alpha = 0;
            double tangentAlpha = (c.Y - b.Y) / (c.X - b.X);
            double Peta = Math.Atan(tangentAlpha);

            if (c.X > b.X)
            {
                alpha = 90 - (Peta * (180 / Math.PI));
            }
            else if (c.X < b.X)
            {
                alpha = 270 - (Peta * (180 / Math.PI));
            }
            else
            {
                if (c.Y > b.Y) alpha = 0;
                if (c.Y < b.Y) alpha = 180;
            }
            return alpha;
        }

      
        private void winformsMap1_MouseMove(object sender, MouseEventArgs e)
        {
            //Displays the X and Y in screen coordinates.
            statusStrip1.Items["toolStripStatusLabelScreen"].Text = "X:" + e.X + " Y:" + e.Y;

            //Gets the PointShape in world coordinates from screen coordinates.
            PointShape pointShape = ExtentHelper.ToWorldCoordinate(winformsMap1.CurrentExtent, new ScreenPointF(e.X, e.Y), winformsMap1.Width, winformsMap1.Height);

            //Displays world coordinates.
            statusStrip1.Items["toolStripStatusLabelWorld"].Text = "(world) X:" + Math.Round(pointShape.X, 4) + " Y:" + Math.Round(pointShape.Y, 4);
        }
        
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

    }
}
