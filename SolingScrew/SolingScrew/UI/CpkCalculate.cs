using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SolingScrew.UI
{
    public partial class CpkCalculate : Form
    {
        DataTable mDt = new DataTable();
        
        private string pointCol = "点位";
        private string niuliCol = "扭力值(kgf.cm)";
        private string upLimitCol = "上限(kgf.cm)";
        private string downLimitCol = "下限(kgf.cm)";
        private string resultCol = "结果";
        public CpkCalculate()
        {
            InitializeComponent();
        }
        
        private void CpkCalculate_Load(object sender, EventArgs e)
        {
            TableInit();
        }
        private void TableInit()
        {
            //表格列的初始化
            DataColumn col1 = new DataColumn(pointCol, typeof(string));
            DataColumn col2 = new DataColumn(niuliCol, typeof(string));
            DataColumn col3 = new DataColumn(upLimitCol, typeof(string));
            DataColumn col4 = new DataColumn(downLimitCol, typeof(string));
            DataColumn col5 = new DataColumn(resultCol, typeof(string));
            // 表格添加列
            mDt.Columns.Add(col1);
            mDt.Columns.Add(col2);
            mDt.Columns.Add(col3);
            mDt.Columns.Add(col4);
            mDt.Columns.Add(col5);
            
            //设置列表头30列
            for(int i = 0; i < 31; i++)
            {
                DataColumn col = null;
                
                if(i == 0)
                {
                    col = new DataColumn(string.Format("  ", i), typeof(string));
                }
                else
                {
                    col = new DataColumn(string.Format("点{0}", i), typeof(string));
                }
                
                mDt.Columns.Add(col);
            }
            
            //添加行
            for(int i = 0; i < 5; i++)
            {
                DataRow row = mDt.NewRow();
                mDt.Rows.Add(row);
            }
            
            dataGridView1.DataSource = mDt;
            //dataGridView1.ReadOnly = false;
            dataGridView1.RowHeadersVisible = false;
            //dataGridView1.AllowUserToOrderColumns = false;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            //DataGridView1.AllowUserToResizeColumns = false;   // 禁止用户改变DataGridView1的所有列的列宽
            dataGridView1.AllowUserToResizeRows = false;    //禁止用户改变DataGridView1の所有行的行高
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;    // 禁止用户改变列头的高度
            dataGridView1.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            //dataGridView1.Columns[0].FillWeight = 10;      //第一列的相对宽度为10%
            dataGridView1.Rows[0].Cells[0].Value = "x1";
            dataGridView1.Rows[1].Cells[0].Value = "z1";
            dataGridView1.Rows[2].Cells[0].Value = "y";
            dataGridView1.Rows[3].Cells[0].Value = "x2";
            dataGridView1.Rows[4].Cells[0].Value = "z2";
            
            for(int i = 0; i < dataGridView1.Columns.Count; i++)
            {
                if(i == 0)
                {
                    dataGridView1.Columns[i].ReadOnly = true;
                }
                
                dataGridView1.Columns[i].Width = 60;
                dataGridView1.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
            }
        }
        
        private void button1_Click(object sender, EventArgs e)
        {
            DataRow newRow = mDt.NewRow();
            newRow["点位"] = string.Format("点{0}", 1);
            newRow["扭力值(kgf.cm)"] = 6;
            newRow["上限(kgf.cm)"] = 8;
            newRow["下限(kgf.cm)"] = 10;
            newRow["结果"] = 12;
            mDt.Rows.Add(newRow);
            //int index = dataGridView1.Rows.Add();
            //dataGridView1.Rows[index].Cells[0].Value = string.Format("点{0}", 1);
            //dataGridView1.Rows[index].Cells[1].Value = 2;
            //dataGridView1.Rows[index].Cells[2].Value = 4;
            //dataGridView1.Rows[index].Cells[3].Value = 6;
            //dataGridView1.Rows[index].Cells[4].Value = 8;
        }
        
    }
}
