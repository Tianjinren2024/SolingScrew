using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolingScrew.UI
{
    class Product
    {
        public enum ProductEnum
        {
            Product001, //旋转螺丝机
            Product002, //平面螺丝机
            Product003, //双电批螺丝机
        }
        private static ProductEnum curProduct = ProductEnum.Product002;    //默认旋转螺丝机
        private static Product productInstance = null;
        
        internal static ProductEnum CurProduct
        {
            get
            {
                return curProduct;
            }
            set
            {
                curProduct = value;
            }
        }
        
        private Product()
        {
        }
        public static Product GetProductInstance()
        {
            if(productInstance == null)
            {
                productInstance = new Product();
            }
            
            return productInstance;
        }
        
        public static ProductEnum GetCurProduct()
        {
            return CurProduct;
        }
    }
}
