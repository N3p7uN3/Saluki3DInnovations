using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Printer_Controller
{
    public class GPolygon
    {
        /*
         * This class represents the recangular polygon that each G code extrusion represents.  During the layer processing, each potential dot will call WithinPolygon()
         * to determine if the dot is indeed within /this/ polygon.  The function will return true if this is the case.
         * This class is instantiated with the beginning and end X/Y coordinates of the extrusion.  It will automatically represent the locations of all 4 points
         * of the rectangle within a virtual 2D plane, of which the LayerProcessing can use.
         * 
         * */
        private double _extrusionWidthHalf;

        private double Ax, Ay;
        private double Bx, By;
        private double Cx, Cy;
        private double Dx, Dy;

        private double rectArea;

        private const int comparisonAccuracy = 8;

        private double widthMultiplier = 2;
        
        public GPolygon(double ax, double ay, double bx, double by, double extrusionWidth)
        {
            _extrusionWidthHalf = (double)(widthMultiplier * extrusionWidth / 2);

            double dx, dy;
            double angle = (double)(Math.PI / 2);
            double xdiv = Math.Abs((double)(ax - bx));
            double ydiv = Math.Abs((double)(ay - by));
            angle = (double)(angle - (Math.Atan((double)(ydiv / xdiv))));
            dx = (double)(_extrusionWidthHalf * Math.Cos(angle));
            dy = (double)(_extrusionWidthHalf * Math.Sin(angle));

            if ((ax < bx) && (ay > by))
            {
                Ax = (double)(ax + dx);
                Ay = (double)(ay + dy);
                Bx = (double)(ax - dx);
                By = (double)(ay - dy);
                Cx = (double)(bx - dx);
                Cy = (double)(by - dy);
                Dx = (double)(bx + dx);
                Dy = (double)(by + dy);
            }
            else if ((ax > bx) && (ay > by))
            {
                Ax = (double)(ax + dx);
                Ay = (double)(ay - dy);
                Bx = (double)(ax - dx);
                By = (double)(ay + dy);
                Cx = (double)(bx - dx);
                Cy = (double)(by + dy);
                Dx = (double)(bx + dx);
                Dy = (double)(by - dy);
            }
            else if ((ax > bx) && (ay < by))
            {
                Ax = (double)(ax - dx);
                Ay = (double)(ay - dy);
                Bx = (double)(ax + dx);
                By = (double)(ay + dy);
                Cx = (double)(bx + dx);
                Cy = (double)(by + dy);
                Dx = (double)(bx - dx);
                Dy = (by - dy);
            }
            else if ((ax < bx) && (ay < by))
            {
                Ax = (ax - dx);
                Ay = (ay + dy);
                Bx = (ax + dx);
                By = (ay - dy);
                Cx = (bx + dx);
                Cy = (by - dy);
                Dx = (bx - dx);
                Dy = (by + dy);
            }
            else if ((ax == bx) && (ay > by))
            {
                Ax = (ax + dx);
                Ay = (ay);
                Bx = (ax - dx);
                By = (ay);
                Cx = (bx - dx);
                Cy = (by);
                Dx = (bx + dx);
                Dy = (by);
            }
            else if ((ax == bx) && (ay < by))
            {
                Ax = (ax - dx);
                Ay = (ay);
                Bx = (ax + dx);
                By = (ay);
                Cx = (by + dx);
                Cy = (by);
                Dx = (bx - dx);
                Dy = (by);
            }
            else if ((ay == by) && (bx < ax))
            {
                Ax = (ax);
                Ay = (ay - dy);
                Bx = (ax);
                By = (ay + dy);
                Cx = (bx);
                Cy = (by + dy);
                Dx = (bx);
                Dy = (by - dy);
            }
            else if ((ay == by) && (bx > ax))
            {
                Ax = (ax);
                Ay = (ay + dy);
                Bx = (ax);
                By = (ay - dy);
                Cx = (bx);
                Cy = (by - dy);
                Dx = (bx);
                Dy = (by + dy);
            }

            
            
            rectArea = (double)((extrusionWidth * widthMultiplier) * GetLength(ax, ay, bx, by));
            rectArea = Math.Round(rectArea, comparisonAccuracy);

            //debug
            if ((Ax == Ay) && (Bx == By) && (Cx == Cy))
            {
                Debug.Print("warning");
            }

        }

        public GPolygonMinMaxes GetMinMaxes()
        {
            List<double> xs = new List<double>();
            List<double> ys = new List<double>();

            xs.Add(Ax);
            xs.Add(Bx);
            xs.Add(Cx);
            xs.Add(Dx);
            xs.Sort();

            ys.Add(Ay);
            ys.Add(By);
            ys.Add(Cy);
            ys.Add(Dy);
            ys.Sort();

            GPolygonMinMaxes minMaxes = new GPolygonMinMaxes();
            minMaxes.XMin = xs[0];
            minMaxes.XMax = xs[3];
            minMaxes.YMin = ys[0];
            minMaxes.YMax = ys[3];

            return minMaxes;



        }

        public bool WithinPolygon(double Px, double Py)
        {
            double tri1, tri2, tri3, tri4, sum;
            tri1 = AreaOfTriangle(Ax, Ay, Px, Py, Dx, Dy);
            tri2 = AreaOfTriangle(Dx, Dy, Px, Py, Cx, Cy);
            tri3 = AreaOfTriangle(Cx, Cy, Px, Py, Bx, By);
            tri4 = AreaOfTriangle(Px, Py, Bx, By, Ax, Ay);
            sum = (double)(tri1 + tri2 + tri3 + tri4);

            if ((tri1.Equals(double.NaN)) || (tri2.Equals(double.NaN)) || (tri3.Equals(double.NaN)) || (tri4.Equals(double.NaN)))
                Debug.Print("Warning: NaN triangle area");
            sum = Math.Round(sum, comparisonAccuracy);

            if (sum > rectArea)
                return false;
            else
                if ((!tri1.Equals(double.NaN)) && (!tri2.Equals(double.NaN)) && (!tri3.Equals(double.NaN)) && (!tri4.Equals(double.NaN)))
                    return true;
                else
                    return false;

        }

        private double AreaOfTriangle(double ax, double ay, double bx, double by, double cx, double cy)
        {
            double side1, side2, side3, s;
            side1 = GetLength(ax, ay, bx, by);
            side2 = GetLength(ax, ay, cx, cy);
            side3 = GetLength(bx, by, cx, cy);
            s = (double)(((double)(side1 + side2 + side3)) / (double)2);

            double term1, term2, term3;
            term1 = (double)(s - side1);
            term2 = (double)(s - side2);
            term3 = (double)(s - side3);

            return Math.Sqrt((double)(s * term1 * term2 * term3));

        }

        private double GetLength(double ax, double ay, double bx, double by)
        {
            return Math.Sqrt((double)(Math.Pow((double)(ax - bx), (double)2) + Math.Pow((double)(ay - by), (double)2)));
        }
    }

    public struct GPolygonMinMaxes
    {
        public double XMin, XMax, YMin, YMax;
    }
}
