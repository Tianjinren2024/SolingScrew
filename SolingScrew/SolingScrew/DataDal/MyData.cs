using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolingScrew.DataDal
{
    struct SingleDpData
    {
        public float x;
        public float y;
        public float z;
        public int dp;
    }
    struct SingleRDpData
    {
        public float x;
        public float y;
        public float z;
        public float u;
        public int dp;
    }
    
    struct DoubleDpData
    {
        public float x1;
        public float x2;
        public float y;
        public float z1;
        public float z2;
        public int m;
        public int dp;
    }
    
    
    class MyData
    {
        public int x1Addr = 2000;
        public int x2Addr = 0;
        public int yAddr;
        public int z1Addr;
        public int z2Addr;
        public int mAddr;
        public int dpAddr;
        
        private static MyData dataInstance = null;
        
        private MyData()
        {
        }
        public static MyData GetDataInstance()
        {
            if(dataInstance == null)
            {
                dataInstance = new MyData();
            }
            
            return dataInstance;
        }
        
        
        
        
        
    }
}
