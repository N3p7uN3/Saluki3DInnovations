using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Printer_Controller
{
    /*
     * This is the end result of the layer processing.  It contains a list of PowderLayer, which is a matrix of booleans representing all the dot locations of the print bed.
     * For each true value, it represents where a dot should be sprayed.  This class also holds the PowderLayer class, represeting a layer of a printed part.
     * 
     * */
    public class PowderPart
    {
        public List<PowderLayer> Part;
        double distanceIndexRatio;
        

        public PowderPart()
        {

            Part = new List<PowderLayer>();
            distanceIndexRatio = 0.264583;
        }
    }

    public class PowderLayer : IComparable
    {
        public bool[,] layer;
        private double _pxSize;
        public int size { get; set; }
        public int LayerNum { get; set; }
        public string debugPoints;

        public PowderLayer(double dotWidth)
        {
            size = Convert.ToInt32(Math.Ceiling(150 / dotWidth));
            layer = new bool[size, size];
            _pxSize = dotWidth;

            //initialize the whole thing to false
            for (int i = 0; i < size; ++i)
            {
                for (int j = 0; j < size; ++j)
                {
                    layer[i, j] = false;
                }
            }

        }

        public double getDist(int px)
        {
            return (double)((double)px * _pxSize);
        }

        #region IComparable Members

        public int CompareTo(object obj)
        {
            PowderLayer comp = (PowderLayer)obj;
            if (comp.LayerNum == this.LayerNum)
                return 0;
            else if (comp.LayerNum > this.LayerNum)
                return -1;
            else
                return 1;
        }

        #endregion
    }
}
