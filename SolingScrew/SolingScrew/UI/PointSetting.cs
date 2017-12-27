//#define WriteToPlc

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
    using SolingScrew.DataDal;
    using SolingScrew.FileOpAPI;
    public partial class PointSetting : Form
    {
        /// <summary>
        /// 读取一个点的值
        /// </summary>
        public enum ReadPointState
        {
            None,       //读取实时点位坐标
            ReadX1,     //读取点的X1坐标值
            ReadZ1,     //读取点的Z1坐标值
            ReadY,      //读取点的Y坐标值
            ReadX2,     //读取点的X2坐标值
            ReadZ2,     //读取点的Z2坐标值
            ReadDp,     //读取电批
            Readm,      //读取面
        }
        public enum WriteCoordCmd
        {
            None,
            CoordX,
            CoordY,
            CoordZ,
            CoordU,
            CoordD,
        }
        public struct OnePoint
        {
            public string x1;
            public string z1;
            public string y;
            public string x2;
            public string z2;
            public string dp;
            public string m;
        }
        
        private static int rowN = 5;
        private string curProductName = string.Empty;   //当前选择的产品名称
        private string curPoint = string.Empty;         //当前选择的点位
        private string curOpProduct = string.Empty;     //当前操作的产品
        private ReadPointState curReadCoordinate = ReadPointState.None;//当前正在读取的坐标
        
        List<string> productNameList = new List<string>();  //产品名称的链表
        List<string> pointList = new List<string>();  //产品点位信息链表
        List<OnePoint> pointsList = new List<OnePoint>();   //点信息链表
        List<string> pointlist = new List<string>();
        
        DataTable mDt = new DataTable();
        //Product curPro = Product.GetProductInstance();
        SerialComm comm = SerialComm.GetScomInstance();
        IniFileHelper iniFileOp = new IniFileHelper();
        private delegate void ShowPointEventHandler(string[] point);
        
        bool isReadPoint = false;
        private float[] curRead = new float[] { 0f, 0f, 0f, 0f};//新点数据缓存
        private float[] offset = new float[] { 0f, 0f, 0f, 0f };//偏差缓存
        public PointSetting()
        {
            InitializeComponent();
        }
        
        private void PointSetting_Load(object sender, EventArgs e)
        {
            if(Product.CurProduct == Product.ProductEnum.Product002)
            {
                rowN = 4;
                label12.Visible = false;
                label13.Visible = false;
                z1Pos.Visible = false;
            }
            else if(Product.CurProduct == Product.ProductEnum.Product003)
            {
                rowN = 7;
            }
            
            TableInit();
            LoadData();
            offsetBtn.Enabled = false;
            readTimer.Enabled = true;   //便能读取定时器
        }
        
        /// <summary>
        /// 接收数据的处理，主要用来接收各轴的实时点位值
        /// </summary>
        /// <param name="data"></param>
        private void DataReceived(string[] data)
        {
            if(data.Length > 0)
            {
                if(true == this.InvokeRequired)
                {
                    this.Invoke(new ShowPointEventHandler(ShowPointVal), new object[] { data });
                }
                else
                {
                    ShowPointVal(data);
                }
            }
        }
        
        private void ReadCurPointData()
        {
            string[] dat = null;
            dat = comm.ReadDMDats(dm250, 4);
            
            if(dat != null && dat.Length == 4)
            {
                x1Pos.Text = dat[0];
                x2Pos.Text = dat[1];
                yPos.Text = dat[2];
                curRead[0] = Convert.ToSingle(dat[0]);
                curRead[1] = Convert.ToSingle(dat[1]);
                curRead[2] = Convert.ToSingle(dat[2]);
                curRead[3] = 0f;
                
                if(Product.CurProduct == Product.ProductEnum.Product001)
                {
                    z1Pos.Text = dat[3];
                    curRead[3] = Convert.ToSingle(dat[3]);
                }
            }
        }
        
        private void ReadPointData()
        {
            int i = 0;
            string[] dat = null;
            dat = comm.ReadDMIntDat(1010);
            
            if(dat != null)
            {
                i = Convert.ToInt32(dat[0]);
                
                if(i == 0)
                {
                    i = 1;
                }
            }
            
            dat = comm.ReadDMDats(dm250, 4);
            
            if(dat != null)
            {
                point.x1 = dat[0];
                point.z1 = dat[1];
                point.y = dat[2];
                point.x2 = dat[3];
            }
            
            pointsList.Add(point);
            
            if(pointsList.Count <= 30 && pointsList.Count > 0)
            {
                pointIndex++;
                //加载读取到的点位信息到表格中去
                dataGridView1.Rows[0].Cells[i].Value = point.x1;
                dataGridView1.Rows[1].Cells[i].Value = point.z1;
                dataGridView1.Rows[2].Cells[i].Value = point.y;
                dataGridView1.Rows[3].Cells[i].Value = point.x2;
                
                if(Product.CurProduct == Product.ProductEnum.Product001)
                {
                    dataGridView1.Rows[4].Cells[i].Value = point.z2;
                }
            }
            
            point.x1 = "";
            point.x2 = "";
            point.y = "";
            point.x2 = "";
            point.z2 = "";
        }
        
        private void ReadPointsData()
        {
            string[] dat = null;
            point.x1 = "";
            point.z1 = "";
            point.y = "";
            point.x2 = "";
            point.z2 = "";
            pointIndex = 0;
            
            for(int i = 0; i < 30; i++)     //读取30个点
            {
                dat = comm.ReadDMDat(dm2000 + pointIndex * 2);
                
                if(dat != null)
                {
                    point.x1 = dat[0];
                }
                
                dat = comm.ReadDMDat(dm2000 + 100 + pointIndex * 2);
                
                if(dat != null)
                {
                    point.z1 = dat[0];
                }
                
                dat = comm.ReadDMDat(dm2000 + 200 + pointIndex * 2);
                
                if(dat != null)
                {
                    point.y = dat[0];
                }
                
                if(Product.CurProduct == Product.ProductEnum.Product001)
                {
                    dat = comm.ReadDMDat(dm2000 + 300 + pointIndex * 2);
                    
                    if(dat != null)
                    {
                        point.x2 = dat[0];
                    }
                    
                    dat = comm.ReadDMIntDat(dm2000 + 400 + pointIndex * 2);
                    
                    if(dat != null)
                    {
                        point.z2 = dat[0];
                    }
                }
                else if(Product.CurProduct == Product.ProductEnum.Product002)
                {
                    dat = comm.ReadDMIntDat(dm2000 + 400 + pointIndex * 2);
                    
                    if(dat != null)
                    {
                        point.x2 = dat[0];
                    }
                }
                
                pointsList.Add(point);
                pointIndex++;
                //加载读取到的点位信息到表格中去
                int col = i + 1;
                dataGridView1.Rows[0].Cells[col].Value = point.x1;
                dataGridView1.Rows[1].Cells[col].Value = point.z1;
                dataGridView1.Rows[2].Cells[col].Value = point.y;
                dataGridView1.Rows[3].Cells[col].Value = point.x2;
                
                if(Product.CurProduct == Product.ProductEnum.Product001)
                {
                    dataGridView1.Rows[4].Cells[col].Value = point.z2;
                }
            }
        }
        
        OnePoint point = new OnePoint();
        private void ShowPointVal(string[] points)
        {
        }
        
        private void ReadOnePointInfo()
        {
        }
        
        private TextBox control;
        
        public string CurOpProduct
        {
            get
            {
                return curOpProduct;
            }
            set
            {
                curOpProduct = value;
            }
        }
        
        /// <summary>
        /// 表格只输入数值类型的处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridView1_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            //只对TextBox类型的单元格进行验证
            if(e.Control.GetType().BaseType.Name == "TextBox")
            {
                control = new TextBox();
                control = (TextBox)e.Control;
                control.KeyPress += new KeyPressEventHandler(control_KeyPress);
                //if(control.Text == "")     //需要限制输入数字的单元格
                //{
                //    control.KeyPress += new KeyPressEventHandler(control_KeyPress);
                //}
                //else
                //{
                //    //非数字类型单元格
                //    control.Leave += new EventHandler(control_Leave);
                //}
            }
        }
        /// <summary>
        /// textbox按键输入事件响应
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void control_KeyPress(object sender, KeyPressEventArgs e)
        {
            //限制只能输入0-9的数字，退格键:8 小数点:46 和回车 -:45
            if((e.KeyChar >= '0' && e.KeyChar <= '9') || e.KeyChar == '-' || e.KeyChar == 13 || e.KeyChar == 8 || e.KeyChar == 46)//((int)e.KeyChar >= 48 && (int)e.KeyChar <= 57)
            {
                if(control.Text == "0" && e.KeyChar == '0')//防止输入以００开头
                {
                    e.Handled = true;
                }
                else if((control.Text == "" || control.Text.Contains('.')) && e.KeyChar == '.')//开始输入时防止首输为点及重复点
                {
                    e.Handled = true;
                }
                else if((control.Text.Contains('-') && e.KeyChar == '-') || (control.Text == "-" && e.KeyChar == '.'))//防止首输为'－'时接着输入'.',及重复输入'-'
                {
                    e.Handled = true;
                }
                //else if(control.Text.Contains('.') && ((control.Text.IndexOf('.') + 3) == control.Text.Length) && e.KeyChar != 8 && e.KeyChar != 13)
                else if(control.Text.Contains('.') && (control.Text.IndexOf('.') == (control.Text.Length - 3)) && e.KeyChar != 8 && e.KeyChar != 13)
                {
                    e.Handled = true;
                }
                else if(control.Text.Length > 0 && e.KeyChar == '-')    //防止编辑已有数据时可以输入符号-的不足
                {
                    e.Handled = true;
                }
                else
                {
                    e.Handled = false;
                }
            }
            else
            {
                e.Handled = true;
                //MessageBox.Show("只能输入数字！");
            }
        }
        
        void control_Leave(object sender, EventArgs e)
        {
            //如果需要限制字符串输入长度
            //if(control.Text.Length != 11)
            //{
            //    MessageBox.Show("只能为位！");
            //    control.Focus();
            //}
        }
        
        /// <summary>
        /// 点位设置表格的初始化
        /// </summary>
        private void TableInit()
        {
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
            for(int i = 0; i < rowN; i++)
            {
                DataRow row = mDt.NewRow();
                mDt.Rows.Add(row);
            }
            
            dataGridView1.DataSource = mDt;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            //dataGridView1.AllowUserToOrderColumns = false;
            //DataGridView1.AllowUserToResizeColumns = false;   // 禁止用户改变DataGridView1的所有列的列宽
            dataGridView1.AllowUserToResizeRows = false;    //禁止用户改变DataGridView1の所有行的行高
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;    // 禁止用户改变列头的高度
            dataGridView1.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            dataGridView1.Rows[0].Cells[0].Value = "x";
            dataGridView1.Rows[1].Cells[0].Value = "y";
            dataGridView1.Rows[2].Cells[0].Value = "z";
            
            if(Product.CurProduct == Product.ProductEnum.Product001)
            {
                dataGridView1.Rows[3].Cells[0].Value = "u";
                dataGridView1.Rows[4].Cells[0].Value = "电批";
            }
            else if(Product.CurProduct == Product.ProductEnum.Product002)
            {
                dataGridView1.Rows[3].Cells[0].Value = "电批";
            }
            
            //dataGridView1.Rows[5].Cells[0].Value = "电批";
            //dataGridView1.Rows[6].Cells[0].Value = "面";
            
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
        
        private void ClearTable()
        {
            int rows = dataGridView1.RowCount;
            
            for(int i = 0; i < rows; i++)
            {
                for(int j = 1; j < 31; j++)
                {
                    dataGridView1.Rows[i].Cells[j].Value = string.Empty;
                }
            }
            
            pointIndex = 0;
            pointsList.Clear();
        }
        
        /// <summary>
        /// 加载数据
        /// </summary>
        private void LoadData()
        {
            UpdateProductCombo();
        }
        
        /// <summary>
        /// 更新产品列表框
        /// </summary>
        private void UpdateProductCombo()
        {
            //加载产品名称
            List<string> secList = new List<string>();
            secList = iniFileOp.ReadSections();
            productNameList.Clear();
            
            for(int i = 0; i < secList.Count; i++)
            {
                if(secList[i] == "SysSetting" || secList[i] == "CurOpProduct")
                {
                    secList.RemoveAt(i);
                }
            }
            
            if(secList.Count <= 0)
            {
                return;
            }
            
            foreach(string sec in secList)
            {
                productNameList.Add(sec);
            }
            
            productCombo.Items.AddRange(secList.ToArray());
            curProductName = iniFileOp.ReadString("CurOpProduct", "ProductName", string.Empty); ;//secList[0];
            productCombo.SelectedIndex = productCombo.FindString(curProductName);
            UpdatePointCombo();
        }
        
        private int dm250 = 250;    //读取点位的当前值的起始地址
        private int dm2000 = 2000;  //点位存储的首地址
        private int pointIndex = 0; //读取的点位的索引
        
        private void readTimer_Tick(object sender, EventArgs e)
        {
            #if WriteToPlc
        
            if(sendToPlcFlag)
            {
                WritePointsToPLC();
            }
            else
            {
                ReadCurPointData();
            }
            
            #else
            ReadCurPointData();
            #endif
        }
        
        /// <summary>
        /// 更新点位列表框
        /// </summary>
        private void UpdatePointCombo()
        {
            //加载当前产品的点位信息
            List<string> pList = new List<string>();
            pointCombo.Items.Clear();
            pointCombo.SelectedIndex = -1;
            pointCombo.Text = "";
            pList = iniFileOp.ReadKeys(curProductName);
            pointList = pList;  //点的名称链表
            
            if(pList.Count <= 0)
            {
                return;
            }
            
            pointCombo.Items.AddRange(pList.ToArray());
            pointCombo.SelectedIndex = 0;
            AddDataToTable();
        }
        
        
        private void AddDataToTable()
        {
            ClearTable();
            pointlist.Clear();
            
            //加载点位信息到表格中去
            for(int i = 0; i < pointList.Count; i++)
            {
                string val = iniFileOp.ReadValue(curProductName, pointList[i]);
                pointlist.Add(val);
                string[] pointUnit = val.Split(',');
                
                for(int j = 0; j < pointUnit.Count(); j++)
                {
                    dataGridView1.Rows[j].Cells[i + 1].Value = pointUnit[j];
                }
            }
        }
        
        private bool sendToPlcFlag = false;
        List<string> curPointList = new List<string>();
        private static int addrX1 = 2000;  //x1在PLC DM域内的起始地址
        private static int addrZ1 = addrX1 + 100;
        private static int addrY = addrX1 + 200;
        private static int addrX2 = addrX1 + 300;
        private static int addrZ2 = addrX1 + 400;
        private static int addrDp = addrX1 + 500;
        private static int addrPe = addrX1 + 600;
        
        
        private List<string> GetPointInfoByProduct(string product)
        {
            //加载当前产品的点位信息
            List<string> pList = new List<string>();
            pList = iniFileOp.ReadValues(product);
            return pList;
        }
        
        private void switchBtn_Click(object sender, EventArgs e)
        {
            string section = "CurOpProduct";    //当前操作的产品信息
            string key = "ProductName";
            
            //只有当产品数大于0时才能切换
            if(productCombo.Items.Count > 0)
            {
                CurOpProduct = productCombo.SelectedItem.ToString();
                iniFileOp.WriteString(section, key, CurOpProduct);
                iniFileOp.WriteString(section, "CurProIndex", productCombo.SelectedIndex.ToString());
                //this.DialogResult = DialogResult.OK;
                #if WriteToPlc
                curPointList = GetPointInfoByProduct(curProductName);
                sendToPlcFlag = true;   //切换产品后要重新发送点位信息到PLC
                //MessageBox.Show("产品切换完成，请回到主界面以完成写入！", "温馨提示", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                #endif
            }
            else
            {
                MessageBox.Show("请先输入产品名称", "温馨提示", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        /// <summary>
        /// 添加新产品事件响应
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void newBtn_Click(object sender, EventArgs e)
        {
            string title = "新增产品";
            SingleInputDlg inputWin = new SingleInputDlg(title);
            inputWin.SetTitle(title);
            inputWin.SetNameList(productNameList);
            inputWin.StartPosition = FormStartPosition.CenterParent;
            
            if(inputWin.ShowDialog() == DialogResult.OK)
            {
                productNameList.Add(inputWin.NewName);
                productCombo.Items.Add(inputWin.NewName);
                productCombo.SelectedIndex = productCombo.Items.Count - 1;
            }
        }
        
        private void delBtn_Click(object sender, EventArgs e)
        {
            //有产品时才响应删除
            if(productCombo.Items.Count > 0)
            {
                //先提示用户是否确认删除操作
                DialogResult res = MessageBox.Show("您确定要删除该产品吗?", "温馨提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
                
                if(res == DialogResult.OK)
                {
                    int index = productCombo.SelectedIndex;
                    productCombo.Items.RemoveAt(index);
                    productNameList.RemoveAt(index);
                    //包含则从文件中删除
                    iniFileOp.ClearSection(curProductName);
                    
                    //并及时更新显示
                    if(productCombo.Items.Count > 0)
                    {
                        productCombo.SelectedIndex = 0;
                        curProductName = productCombo.SelectedItem.ToString();
                    }
                    else
                    {
                        productCombo.Items.Clear();
                        productCombo.Text = "";
                        curProductName = "";
                    }
                }
            }
        }
        
        private void shieldPointBtn_Click(object sender, EventArgs e)
        {
        }
        
        private void cancelShieldBtn_Click(object sender, EventArgs e)
        {
        }
        
        //删除产品按键响应事件
        private void delPointBtn_Click(object sender, EventArgs e)
        {
            //先判断是否包含
            if(pointCombo.Items.Count > 0)
            {
                DialogResult res = MessageBox.Show("您确定要删除该点吗?", "温馨提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
                
                if(res == DialogResult.OK)
                {
                    int index = pointCombo.SelectedIndex;
                    pointCombo.Items.RemoveAt(index);
                    pointList.RemoveAt(index);
                    
                    for(int i = 0; i < rowN; i++)
                    {
                        dataGridView1.Rows[i].Cells[index + 1].Value = "";
                    }
                    
                    iniFileOp.DeleteKey(curProductName, curPoint);
                    
                    //并及时更新显示
                    if(pointCombo.Items.Count > 0)
                    {
                        pointCombo.SelectedIndex = 0;
                        curPoint = pointCombo.SelectedItem.ToString();
                    }
                    else
                    {
                        pointCombo.Items.Clear();
                        pointCombo.Text = "";
                    }
                }
            }
        }
        
        private void changeProgramBtn_Click(object sender, EventArgs e)
        {
            ClearTable();
        }
        
        private void productCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            curProductName = productCombo.SelectedItem.ToString();
            UpdatePointCombo();
        }
        
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            curPoint = pointCombo.SelectedItem.ToString();
        }
        
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
        }
        
        private void saveBtn_Click(object sender, EventArgs e)
        {
            pointList.Clear();  //保存前先清空点链表信息缓存
            
            //先判断当前产品名称是否选择或是否存在
            if(productCombo.SelectedIndex >= 0)
            {
                //再判断当前表格中输入的信息，扫描所有的30列
                for(int col = 1; col < 31; col++)
                {
                    string point = string.Empty;
                    List<string> pointUnit = new List<string>();
                    
                    //获取一个点的信息
                    for(int row = 0; row < rowN; row++)
                    {
                        string temp = dataGridView1.Rows[row].Cells[col].Value.ToString();
                        
                        //判断当前单元格的内容是否为空，不为空时才加入单元点集合中去
                        if(!string.IsNullOrEmpty(temp) && temp != "")
                        {
                            pointUnit.Add(temp);
                            
                            if(row == (rowN - 1))
                            {
                                point += temp;
                            }
                            else
                            {
                                point += temp + ',';
                            }
                        }
                    }
                    
                    if(pointUnit.Count <= 0)
                    {
                        //当前扫描列输入为空时跳过
                    }
                    else
                    {
                        if(pointUnit.Count < rowN)
                        {
                            MessageBox.Show("表中点位信息输入不全，请先补全！", "温馨提示", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            return;
                        }
                        
                        pointList.Add(point);   //当输入全时再添加到链表中去
                    }
                }
                
                SavePointData();    //保存
                UpdatePointComboItems();//更新
            }
        }
        
        /// <summary>
        /// 保存
        /// </summary>
        private void SavePointData()
        {
            string product = string.Empty;
            string point = string.Empty;
            product = productCombo.SelectedItem.ToString();
            pointCombo.Items.Clear();//更新前先清空
            
            for(int i = 0; i < pointList.Count; i++)
            {
                point = string.Format("Point{0}", i + 1);
                pointCombo.Items.Add(point);
                iniFileOp.WriteString(product, point, pointList[i]);
            }
            
            pointCombo.SelectedIndex = 0;
        }
        
        /// <summary>
        /// 更新点位下拉控件
        /// </summary>
        private void UpdatePointComboItems()
        {
        }
        
        /// <summary>
        /// 窗口关闭前的处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PointSetting_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(sendToPlcFlag)
            {
                MessageBox.Show("参数正在写入，请稍后在关闭！", "温馨提示", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            
            readTimer.Stop();
            readTimer.Enabled = false;
            this.DialogResult = DialogResult.OK;
        }
        
        /// <summary>
        /// 读点位信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void readPointBtn_Click(object sender, EventArgs e)
        {
            if(comm.GetScomState())
            {
                ReadPointsData();
                isReadPoint = true;
                offsetBtn.Enabled = true;
            }
        }
        
        
        private void offsetBtn_Click(object sender, EventArgs e)
        {
            if(!isReadPoint)
            {
                //MessageBox.Show("请先读取新的点的数据再计算偏差！", "温馨提示", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            
            //只有在该点有数据时才能进行偏差计算
            if(pointlist.Count > 0)
            {
                string[] pointUnit = pointlist[0].Split(',');
                offset[0] = curRead[0] - Convert.ToSingle(pointUnit[0]);
                offset[1] = curRead[1] - Convert.ToSingle(pointUnit[1]);
                offset[2] = curRead[2] - Convert.ToSingle(pointUnit[2]);
                offset[3] = curRead[3] - Convert.ToSingle(pointUnit[3]);
            }
            
            ClearTable();
            
            //加载点位信息到表格中去
            for(int i = 0; i < pointlist.Count; i++)
            {
                string[] pointUnit = pointlist[i].Split(',');
                
                for(int j = 0; j < rowN - 1; j++)
                {
                    dataGridView1.Rows[j].Cells[i + 1].Value = (Convert.ToSingle(pointUnit[j]) + offset[j]).ToString("f2");
                }
            }
            
            //test
            //float x = 4.56f;
            //string str = x.ToString();
            //float y = Convert.ToSingle(str);
        }
    }
}
