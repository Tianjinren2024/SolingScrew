#define Synch

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
//using System.IO.Ports;


namespace SolingScrew
{
    using SolingScrew.UI;
    using SolingScrew.DataDal;
    using SolingScrew.XYGraph;
    using SolingScrew.FileOpAPI;
    
    public partial class solingScrew : Form
    {
        /// <summary>
        /// 读取PLC控制状态信息枚举
        /// </summary>
        public enum ReadPlcCtrlCmd
        {
            None,
            ReadW3300,  //PLC启动工控机状态的读取
            ReadW3301,  //是否打完一个螺丝的状态读取
            ReadW3302,  //PLC是否启动工控机采集扭矩状态的读取
            ReadW3303,  //参数写入
        }
        /// <summary>
        /// 读取当前坐标各点的值
        /// </summary>
        public enum ReadPointCmd
        {
            None,
            ReadX1D250,  //读取当前的X1坐标值
            ReadZ1D252,  //读取当前的Z1坐标值
            ReadYD254,   //读取当前的Y坐标值
            ReadX2D256,  //读取当前的X2坐标值
            ReadZ2D258,  //读取当前的Z2坐标值
        }
        /// <summary>
        /// 读取错误报警信息的指令枚举
        /// </summary>
        public enum ReadErrorCmd
        {
            None,
            ReadW20000,  //供料1无料
            ReadW20001,  //供料2无料
            ReadW20002,  //真空检测1异常
            ReadW20003,  //真空检测2异常
            ReadW20004,  //打螺丝1异常
            ReadW20005,  //打螺丝2异常
            ReadW20006,  //无物料
            
            ReadW20201,  //伺服电机异常
            ReadW20202,  //电批1报错A
            ReadW20203,  //电批2报错B
            
            ReadW20300,  //轴控模块X轴异常
            ReadW20301,  //轴控模块Y轴异常
            
            ReadW20400,  //急停中
            ReadW20405,  //X1平移伺服异常
            ReadW20406,  //Y轴伺服异常
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
        
        private int curveXnum = 20; //曲线横坐标的刻度数
        private int xInterval = 1;
        private int totalScrewNum = 0;  //总的锁螺丝数
        private int passNum = 0;    //通过的数量
        private int failNum = 0;    //失败的数量
        private float passRate = 0; //直通率
        float torsionUp = 0;        //扭力上限
        float torsionDown = 0;      //扭力下限
        private int baudRate = 19200;   //电批控制器的波特率
        
        private bool hasCoordXChanged = false;   //坐标x轴重画标志
        private bool nextPoint = false; //下一点实时曲线绘制的判断
        private bool nextProduct = false;   //下一个产品
        private bool readStatusFlag = false;    //读取PLC的状态信息
        
        private WriteCoordCmd curCoordCmd = WriteCoordCmd.None;
        
        private int screenH = 0;
        private int screenW = 0;    //屏幕分辨率
        
        private int startTestTime = 0;  //开始测试时间
        private int endTestTime = 0;    //结束测试时间
        private bool startPCFlag = false;   //启动工控机标志
        private bool isStart = false;   //是否开始打螺丝
        private bool readTorsionEnableFlag = false;     //扭力采集便能标志
        private bool completeOneScrewFlag = false;      //打完一个螺丝
        private bool completeOneProductFlag = false;    //完成一个产品
        
        private string curProduct = string.Empty;   //当前操作的产品
        private bool sendToPlcFlag = true;  //是否发送点位信息到PLC,只有在刚打开上位机或切换产品时才需发送点位信息
        private List<string> curPointList = new List<string>(); //从系统设置文件中读到的点位信息的缓存链表
        
        //对象的实例化
        DataTable mDt = new DataTable();
        XYChat torsionCurve = new XYChat();
        IniFileHelper iniFileOp = new IniFileHelper();
        ModbusTcpDev dianpi1 = new ModbusTcpDev("192.168.0.1");
        ModbusTcpDev dianpi2 = new ModbusTcpDev("192.168.0.2");
        SerialComm comm = SerialComm.GetScomInstance();
        ModbusRtuDev rtuCom = ModbusRtuDev.GetRtuInstance();
        //ProExpertDev modTcp = ProExpertDev.GetTcpInstance();
        
        private delegate void ModbusDataHandler(string dat);
        private delegate void ShortDataHandler(short addr, List<short> list);
        private delegate void WrDataHandler(string[] dat);
        private delegate void DmDataHandler(string[] dat);
        public solingScrew()
        {
            this.StartPosition = FormStartPosition.CenterScreen;
            InitializeComponent();
            //开启双缓冲，防止最小化后再显示时闪屏或重绘（不起作用，可能双缓冲不适用这种情况），
            //this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            //this.UpdateStyles();
        }
        
        private void solingScrew_Load(object sender, EventArgs e)
        {
            screenW = Screen.PrimaryScreen.Bounds.Width;
            screenH = Screen.PrimaryScreen.Bounds.Height;   //获取屏幕分辨率
            //this.Width = screenW;
            //this.Height = screenH;
            //this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            InitProductInfo();                                  //初始化产品信息
            GetTorsionLimit(ref torsionDown, ref torsionUp);    //从设置文件中获取扭力的上下限
            comm.ScomInit();
            rtuCom.InitSerialPort(baudRate);
            //dianpi1.ModbusTcpInit();
            TableInit();
            XYCharInit((int)torsionUp + 1);
            
            if(comm.GetScomState())
            {
                listBox1.Items.Add("串口1打开成功");
                //comm.scomDataReceived += new SerialComm.ScomDataReceivedHandler(CommDataReceived);
                //comm.bitDataReceived += new SerialComm.BitDataReceivedHandler(WrDataReceived);
                //comm.wordDataReceived += new SerialComm.WordDataReceivedHandler(DmDataReceived);
                tcpTimer.Enabled = true;    //串口打开成功就要向下发送数据了，只有同PLC的串口通信上了，才会有后续的扭力读取等操作，所以在此打开定时器
            }
            else
            {
                listBox1.Items.Add("串口1打开失败");
            }
            
            if(rtuCom.IsOpen)
            {
                listBox1.Items.Add("串口2打开成功");
            }
            else
            {
                listBox1.Items.Add("串口2打开失败");
            }
            
            tcpTimer.Enabled = true;
        }
        
        private int[] baudTable = new int[5] { 9600, 19200, 38400, 57600, 115200};
        /// <summary>
        /// 初始化产品信息
        /// </summary>
        private void InitProductInfo()
        {
            curProduct = iniFileOp.ReadValue("CurOpProduct", "ProductName");
            productName.Text = curProduct;
            curPointList = GetPointInfoByProduct(curProduct);
        }
        
        private void GetTorsionLimit(ref float down, ref float up)
        {
            try
            {
                baudRate = baudTable[Convert.ToInt32(iniFileOp.ReadValue("SysSetting", "BaudRate"))];
                down = Convert.ToSingle(iniFileOp.ReadValue("SysSetting", "Screw1TorsionDown"));
                up = Convert.ToSingle(iniFileOp.ReadValue("SysSetting", "Screw1TorsionUp"));
            }
            catch
            {
                MessageBox.Show("配置文件打开有误,请检查配置文件有效性!", "错误提示", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        
        private string chartTitle = "实时扭力值点位波形图";
        private string yName = "扭力值(kgm.cm)";
        private string xName = "点";
        /// <summary>
        /// 扭力曲线图窗口部件的初始化
        /// </summary>
        private void XYCharInit(int ymax)
        {
            if(ymax <= 0)
            {
                ymax = 5;
            }
            
            //chart1.Series.Clear();
            //chart1.Width = 520;                      //图表宽度
            //chart1.Height = 320;                     //图表高度
            chart1.BackColor = Color.WhiteSmoke; //Color.Azure;             //图表背景色
            chart1.Titles.Add(chartTitle);                //图表标题
            chart1.Series[0].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Spline;//设置曲线类型
            chart1.ChartAreas[0].AxisX.LabelAutoFitStyle = LabelAutoFitStyles.None;//1.设置当前X轴Label自动设置格式 = 关闭
            chart1.ChartAreas[0].AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;//2.设置适应全部数据点
            chart1.ChartAreas[0].AxisX.LabelStyle.IsStaggered = false;//3.设置当前X轴Label的双行显示格式 = 关闭
            chart1.ChartAreas[0].AxisX.IsStartedFromZero = true;//4.设置X轴从0开始
            chart1.ChartAreas[0].AxisX.ScrollBar.IsPositionedInside = false;//设置滚动条是在外部显示
            chart1.ChartAreas[0].AxisX.ScrollBar.Enabled = false;
            chart1.ChartAreas[0].AxisX.ScrollBar.Size = 10;//设置滚动条的宽度
            chart1.ChartAreas[0].AxisX.ScrollBar.ButtonStyle = ScrollBarButtonStyles.SmallScroll;//滚动条只显示向前的按钮，主要是为了不显示取消显示的按钮
            chart1.ChartAreas[0].AxisX.ScaleView.Size = curveXnum;//设置图表可视区域数据点数，说白了一次可以看到多少个X轴区域
            chart1.ChartAreas[0].AxisX.LineColor = Color.Blue;           //X轴颜色
            chart1.ChartAreas[0].AxisY.LineColor = Color.Blue;           //Y轴颜色
            chart1.ChartAreas[0].AxisX.LineWidth = 1;                    //X轴宽度
            chart1.ChartAreas[0].AxisY.LineWidth = 1;                    //Y轴宽度
            chart1.ChartAreas[0].AxisX.Interval = xInterval;                     //X轴间隔
            chart1.ChartAreas[0].AxisX.Title = xName;                    //X轴名称
            chart1.ChartAreas[0].AxisY.Title = yName;                    //Y轴名称
            chart1.ChartAreas[0].AxisX.Minimum = 0;     //坐标最小值，这样的话，X轴坐标是从1开始
            chart1.ChartAreas[0].AxisY.Maximum = ymax;     //设置Y轴最大值
            //chart1.ChartAreas[0].AxisY.Minimum = 0;   //设置Y轴最小值
            chart1.ChartAreas[0].AxisX.Enabled = AxisEnabled.True;
            chart1.ChartAreas[0].AxisY.Enabled = AxisEnabled.True;
            chart1.ChartAreas[0].BackColor = Color.Black;
            chart1.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.NotSet;//DashDot;
            chart1.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.NotSet;//DashDot;    //网格线线型设置
            chart1.ChartAreas[0].AxisX.MajorGrid.LineColor = System.Drawing.Color.Gray;
            chart1.ChartAreas[0].AxisY.MajorGrid.LineColor = System.Drawing.Color.Gray;     //网格线颜色设置
            // 线的颜色为红色
            chart1.Series[0].Color = Color.LawnGreen;
            chart1.Series[0].MarkerStyle = MarkerStyle.Circle;   //线条上的数据点标志类型
            chart1.Series[0].MarkerColor = Color.Red;
            chart1.Series[0].IsValueShownAsLabel = false;    //是否显示数据，值作为标签显示在图表中
            chart1.Series[0].IsVisibleInLegend = false;      //是否显示数据说明
            chart1.Series[0].LabelForeColor = Color.Red;
            chart1.Series[0].XValueType = ChartValueType.Auto;//设置X轴绑定值的类型
            chart1.Series[0].MarkerSize = 3;                 //绘制点的标志大小
            chart1.Series[0].Points.AddXY(0, 0);    //初始化数据源，要不不显示坐标轴
            //chart1.Series[0].Points[0].IsEmpty = true;    //断开连线
            //chart1.Series[0].LegendToolTip = "Target Output";//鼠标放到系列上出现的文字
            //chart1.Series[0].LegendText = "Target Output";//系列名字
            //chart1.Series[0].BorderWidth = 1;//设置线宽
            // Y的最大值
            // 隐藏图示
            chart1.Legends[0].Enabled = false;
            //绘制上下限线
            Series seriesUp = new Series();
            Series seriesDown = new Series();
            chart1.Series.Add(seriesUp);
            chart1.Series.Add(seriesDown);
            chart1.Series[1].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            chart1.Series[1].Color = Color.Red;
            chart1.Series[2].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            chart1.Series[2].Color = Color.Red;
            
            for(int i = 0; i <= curveXnum; i++)
            {
                chart1.Series[1].Points.AddXY(i, torsionUp);
                chart1.Series[2].Points.AddXY(i, torsionDown);
            }
        }
        
        private string pointCol = "点位";
        private string niuliCol = "扭力值(kg.cm)";
        private string upLimitCol = "上限(kg.cm)";
        private string downLimitCol = "下限(kg.cm)";
        private string resultCol = "结果";
        
        /// <summary>
        /// 点位信息表格的初始化
        /// </summary>
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
            
            for(int i = 0; i < dataGridView1.Height / dataGridView1.RowTemplate.Height - 2; i++)
            {
                // 添加空白行
                DataRow blankRow = mDt.NewRow();//row["点位"] = "x10001";
                mDt.Rows.Add(blankRow);
            }
            
            dataGridView1.DataSource = mDt;
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            //DataGridView1.AllowUserToResizeColumns = false;   // 禁止用户改变DataGridView1的所有列的列宽
            dataGridView1.AllowUserToResizeRows = false;    //禁止用户改变DataGridView1の所有行的行高
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;    // 禁止用户改变列头的高度
        }
        
        /// <summary>
        /// ModbusTCP通信收到数据事件的处理委托
        /// </summary>
        /// <param name="dat"></param>
        private void ModbusDataReceived(string dat)
        {
            if(true == this.InvokeRequired)
            {
                this.Invoke(new ModbusDataHandler(ShowPointVal), new object[] { dat });
            }
            else
            {
                ShowPointVal(dat);
            }
        }
        private void ShowPointVal(string dat)
        {
            listBox1.Items.Add(dat);
            listBox1.SelectedIndex = listBox1.Items.Count - 1;
        }
        
        private void ShortDataReceived(short addr, List<short> list)
        {
            if(true == this.InvokeRequired)
            {
                this.Invoke(new ShortDataHandler(UpdateTableAndWave), new object[] { addr, list});
            }
            else
            {
                UpdateTableAndWave(addr, list);
            }
        }
        //private int mPointNum = 0;//
        private void UpdateTableAndWave(short addr, List<short> list)
        {
            int rows = dataGridView1.RowCount;   //添加数据前先获取表格中现有的行数
            
            for(int i = 0; i < list.Count; i++)
            {
                int j = rows + i;   //新数据要添加到的行的索引
                float torsion = ((float)list[i] / 10000);
                dataGridView1.Rows[j].Cells[0].Value = string.Format("点{0}", i);
                dataGridView1.Rows[j].Cells[1].Value = torsion;
                dataGridView1.Rows[j].Cells[2].Value = torsionUp;
                dataGridView1.Rows[j].Cells[3].Value = torsionDown;
                totalScrewNum++;
                testNum.Text = totalScrewNum.ToString();
                
                if(torsion >= torsionDown && torsion <= torsionUp)
                {
                    passNum++;
                    dataGridView1.Rows[j].Cells[4].Value = "Pass";
                }
                else
                {
                    failNum++;
                    dataGridView1.Rows[j].Cells[4].Value = "Fail";
                }
                
                passRate = passNum / totalScrewNum * 100;
                straitRate.Text = string.Format("{0}%", passRate.ToString("f2"));   //passRate.ToString("f2");
                //根据最新点位及扭力数据更新波形图
                PointF point = new PointF(j, torsion);
                SetChart(chart1, point);
            }
        }
        /// <summary>
        /// 串口接收到PLC数据事件的响应
        /// </summary>
        /// <param name="e"></param>
        private void CommDataReceived(DataReceivedEventArgs e)
        {
            string str = e.DataReceived;    //System.Text.Encoding.Default.GetString(e.DataRecv);
            listBox1.BeginInvoke(
                new MethodInvoker(() =>     //delegate <=> () => 两者等价，委托的符号表示
            {
                ShowPointVal(str);//输出到主窗口文本控件
            }
                                 )
            );
        }
        
        //WR区位读取信息的返回事件处理
        private void WrDataReceived(string[] dat)
        {
            if(true == this.InvokeRequired)
            {
                this.Invoke(new WrDataHandler(WrDataReceive), new object[] {dat});
            }
            else
            {
                WrDataReceive(dat);
            }
        }
        
        private void ReadPLCStatus()
        {
            string[] dat = null;
            
            if(readStatusFlag)
            {
                dat = comm.ReadWRDat(31, 4, 1);
                
                if(dat != null && dat.Length >= 1)
                {
                    plcStatus[0] = (dat[0] != "00") ? 1 : 0;
                    
                    if(plcStatus[0] > 0)
                    {
                        readTorsionEnableFlag = true;   //使能扭力采集
                    }
                    else
                    {
                        readTorsionEnableFlag = false;  //禁能扭力采集
                    }
                }
                
                dat = comm.ReadWRDat(31, 1, 1);
                
                if(dat != null && dat.Length >= 1)
                {
                    plcStatus[1] = (dat[0] != "00") ? 1 : 0;
                    
                    if(plcStatus[1] > 0)
                    {
                        completeOneScrewFlag = true;    //打完一个螺丝
                    }
                    else
                    {
                        completeOneScrewFlag = false;   //未打完一个螺丝
                    }
                }
                
                dat = comm.ReadWRDat(31, 2, 1);
                
                if(dat != null && dat.Length == 1)
                {
                    plcStatus[2] = (dat[0] == "01") ? 1 : 0;
                    
                    if(plcStatus[2] == 1)
                    {
                        completeOneProductFlag = true;   //完成一个产品
                    }
                    else
                    {
                        completeOneProductFlag = false;  //未完成一个产品
                    }
                }
                
                //dat = comm.ReadWRDat(33, 3, 1);
                //if(dat.Length == 1)
                //{
                //    plcStatus[3] = (dat[0] == "01") ? 1 : 0;
                //}
                /*
                dat = comm.ReadWRDat(33, 0, 4);
                if (dat != null)
                {
                    for (int i = 0; i < dat.Length; i++)  //默认4个连续位置的状态连续读取
                    {
                        if (i < 4)
                        {
                            plcStatus[i] = (dat[i] == "01") ? 1 : 0;
                        }
                    }
                }
                
                
                if (plcStatus[0] == 1)
                {
                    startPCFlag = true;    //启动工控机
                }
                else
                {
                    startPCFlag = false;   //未启动工控机
                }
                
                //对PLC状态返回的处理
                if(plcStatus[1] == 1)
                {
                    completeOneScrewFlag = true;    //打完一个螺丝
                }
                else
                {
                    completeOneScrewFlag = false;   //未打完一个螺丝
                }
                
                if(plcStatus[2] == 1)
                {
                    readTorsionEnableFlag = true;   //使能扭力采集
                }
                else
                {
                    readTorsionEnableFlag = false;  //禁能扭力采集
                }
                if (plcStatus[3] == 1)
                {
                    completeOneProductFlag = true;   //完成一个产品
                }
                else
                {
                    completeOneProductFlag = false;  //未完成一个产品
                }
                */
                //再查询报警信息（要不要先判断是否启动工控机）
                dat = comm.ReadWRDat(200, 0, 12);
                
                if(dat != null)
                {
                    for(int i = 0; i < dat.Length; i++)    //默认7个连续位置的状态连续读取
                    {
                        if(i < 12)
                        {
                            plcErrors[i] = (dat[i] == "01") ? 1 : 0;
                        }
                    }
                }
                
                dat = comm.ReadWRDat(204, 0, 4);
                
                if(dat != null)
                {
                    for(int i = 0; i < dat.Length; i++)      //默认3个连续位置的状态连续读取
                    {
                        if(i < 4)
                        {
                            plcErrors[12 + i] = (dat[i] == "01") ? 1 : 0;
                        }
                    }
                }
                
                ErrorHandle();
            }
        }
        
        int[] plcStatus = new int[4];//读取plc的状态返回值保存数组变量
        
        int[] plcErrors = new int[20];//读取plc的状态返回值保存数组变量
        List<string> errorList = new List<string>();
        /// <summary>
        /// 对接收的位查询结果信息进行解析
        /// </summary>
        /// <param name="dat"></param>
        private void WrDataReceive(string[] dat)
        {
        }
        
        private void ErrorHandle()
        {
            string errorStr = string.Empty;
            errorList.Clear();
            listBox2.Items.Clear();
            listBox2.SelectedIndex = -1;
            
            //错误的显示处理
            if(plcErrors.Length > 0)
            {
                if(plcErrors[0] == 1)
                {
                    errorStr = "上下气缸异常";//"供料1无料";
                    errorList.Add(errorStr);
                }
                
                if(plcErrors[1] == 1)
                {
                    errorStr = "X轴异常";//"供料2无料";
                    errorList.Add(errorStr);
                }
                
                if(plcErrors[2] == 1)
                {
                    errorStr = "Y轴异常"; //"真空检测1异常";
                    errorList.Add(errorStr);
                }
                
                if(plcErrors[3] == 1)
                {
                    errorStr = "Z轴异常"; //"真空检测2异常";
                    errorList.Add(errorStr);
                }
                
                if(plcErrors[4] == 1)
                {
                    errorStr = "U轴异常";//"打螺丝1异常";
                    errorList.Add(errorStr);
                }
                
                if(plcErrors[5] == 1)
                {
                    errorStr = "Z轴不在安全位";// "打螺丝2异常";
                    errorList.Add(errorStr);
                }
                
                if(plcErrors[6] == 1)
                {
                    errorStr = "供料1无料";// "无物料";
                    errorList.Add(errorStr);
                }
                
                if(plcErrors[7] == 1)
                {
                    errorStr = "供料2无料";//"伺服电机异常";
                    errorList.Add(errorStr);
                }
                
                if(plcErrors[8] == 1)
                {
                    errorStr = "打螺丝异常";//"伺服电机异常";
                    errorList.Add(errorStr);
                }
                
                if(plcErrors[9] == 1)
                {
                    errorStr = "螺丝机异常";//"电批1报错A";
                    errorList.Add(errorStr);
                }
                
                //if(plcErrors[10] == 1)
                //{
                //    errorStr = "";//"电批2报错B";
                //    errorList.Add(errorStr);
                //}
                
                if(plcErrors[11] == 1)
                {
                    errorStr = "吸螺丝异常";//"轴控模块X轴异常";
                    errorList.Add(errorStr);
                }
                
                if(plcErrors[12] == 1)
                {
                    errorStr = "急停中";//"轴控模块Y轴异常";
                    errorList.Add(errorStr);
                }
                
                if(plcErrors[13] == 1)
                {
                    errorStr = "X轴触碰极限";//"急停中";
                    errorList.Add(errorStr);
                }
                
                if(plcErrors[18] == 1)
                {
                    errorStr = "Y轴触碰极限";//"X1平移伺服异常";
                    errorList.Add(errorStr);
                }
                
                if(plcErrors[19] == 1)
                {
                    errorStr = "Z轴触碰极限";//"Y轴伺服异常";
                    errorList.Add(errorStr);
                }
                
                if(errorList.Count > 0)
                {
                    string curTime = DateTime.Now.ToString("yyyy-MM-dd  hh:mm:ss");//获取格式化的当前时间
                    
                    foreach(string err in errorList)
                    {
                        listBox2.Items.Add(string.Format("{0} - {1}", curTime, err));
                    }
                    
                    listBox2.SelectedIndex = listBox2.Items.Count - 1;
                }
            }
        }
        
        //DM区字读取信息的返回事件处理
        private void DmDataReceived(string[] dat)
        {
            if(true == this.InvokeRequired)
            {
                this.Invoke(new DmDataHandler(DmDataReceive), new object[] {dat});
            }
            else
            {
                DmDataReceive(dat);
            }
        }
        private void DmDataReceive(string[] dat)
        {
        }
        /// <summary>
        /// 系统设置按钮事件响应
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sysSetBtn_Click(object sender, EventArgs e)
        {
            SysSetting sysSetWin = new SysSetting();
            sysSetWin.StartPosition = FormStartPosition.CenterParent;
            sysSetWin.ShowDialog();
            //SingleShow(sysSetWin);
        }
        
        /// <summary>
        /// 点位设置按钮事件响应
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void posSetBtn_Click(object sender, EventArgs e)
        {
            PointSetting posSetWin = new PointSetting();
            posSetWin.StartPosition = FormStartPosition.CenterParent;
            tcpTimer.Enabled = false;
            sendToPlcFlag = false;
            
            if(posSetWin.ShowDialog() == DialogResult.OK)
            {
                if(posSetWin.CurOpProduct == "" || string.IsNullOrEmpty(posSetWin.CurOpProduct))
                {
                }
                else
                {
                    curProduct = posSetWin.CurOpProduct;
                    productName.Text = curProduct;
                    curPointList.Clear();
                    curPointList = GetPointInfoByProduct(curProduct);
                    sendToPlcFlag = true;   //切换产品后要重新发送点位信息到PLC
                }
                
                tcpTimer.Enabled = true;
            }
        }
        
        /// <summary>
        /// CPK计算按钮事件响应
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CPKBtn_Click(object sender, EventArgs e)
        {
            CpkCalculate cpkCalWin = new CpkCalculate();
            cpkCalWin.StartPosition = FormStartPosition.CenterParent;
            cpkCalWin.ShowDialog();
        }
        
        private void datCleanBtn_Click(object sender, EventArgs e)
        {
            ClearTable();
            ClearChartPoints(chart1);
            listBox1.Items.Clear();
            listBox2.Items.Clear();
        }
        
        /// <summary>
        /// 清空表格数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearTable()
        {
            int rows = dataGridView1.RowCount;
            torsions.Clear();
            
            for(int i = 0; i < rows; i++)
            {
                for(int j = 0; j < 5; j++)
                {
                    dataGridView1.Rows[i].Cells[j].Value = string.Empty;
                    dataGridView1.Rows[i].Cells[j].Style.BackColor = Color.White;
                }
            }
        }
        
        private void quitSysBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void clearBtn_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            listBox2.Items.Clear();
        }
        
        /// <summary>
        /// 显示唯一的窗体
        /// </summary>
        List<Form> formList = new List<Form>();
        private void SingleShow(Form form)
        {
            bool hasform = false;   //判断窗体是否已经弹出，默认false
            
            foreach(Form f in formList) //遍历所有窗体对象
            {
                if(f.Name == form.Name) //判断弹出的窗体是否重复
                {
                    hasform = true; //重复，修改为true
                    f.WindowState = FormWindowState.Normal;
                    f.Focus();  //获取焦点
                }
            }
            
            if(hasform)
            {
                form.Close();
            }
            else
            {
                formList.Add(form); //添加到所有窗体中
                form.ShowDialog();  //并打开该窗体
            }
        }
        
        private void loginBtn_Click(object sender, EventArgs e)
        {
            //rtuCom.ReadHoldReg(1, 22, 1);
        }
        
        private void solingScrew_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult res = MessageBox.Show("您确定要退出程序吗?", "退出确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
            
            if(res == DialogResult.OK)
            {
                comm.ScomClose();
                e.Cancel = false;
            }
            else
            {
                e.Cancel = true;
            }
        }
        
        private static int addrX1 = 2000;  //x1在PLC DM域内的起始地址
        private static int addrZ1 = addrX1 + 100;
        private static int addrY = addrX1 + 200;
        private static int addrX2 = addrX1 + 300;
        private static int addrZ2 = addrX1 + 400;
        private static int addrDp = addrX1 + 500;
        private static int addrPe = addrX1 + 600;
        private static int curSendPoint = 0;    //当前发送点
        
        List<PointF> points = new List<PointF>();
        private static float datX = 0;
        private static float datY = 0;
        List<float> torsions = new List<float>();   //扭力值数据链表
        /// <summary>
        /// 定时器1时间到的处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tcpTimer_Tick(object sender, EventArgs e)
        {
            //串口打开且写点到PLC标志置位，则走写入逻辑，要不就读取PLC状态
            if(comm.GetScomState())
            {
                if(sendToPlcFlag)
                {
                    WritePointsToPLC();
                }
                else
                {
                    ReadPLCStatus();
                    
                    //读取扭力值
                    if(readTorsionEnableFlag)     //使能扭力采集时发送扭力采集指令 || completeOneScrewFlag
                    {
                        isStart = true;
                        
                        if(nextPoint)
                        {
                            nextPoint = false;
                            
                            if(hasCoordXChanged)     //如果x轴坐标发生改变
                            {
                                hasCoordXChanged = false;
                                curveXnum = 20; //重画X轴
                                xInterval = 1;
                                chart1.ChartAreas[0].AxisX.ScaleView.Size = curveXnum;
                                chart1.ChartAreas[0].AxisX.Interval = xInterval;
                                chart1.Series[1].Points.Clear();
                                chart1.Series[2].Points.Clear();
                                
                                for(int i = 0; i <= curveXnum; i++)
                                {
                                    chart1.Series[1].Points.AddXY(i, torsionUp);
                                    chart1.Series[2].Points.AddXY(i, torsionDown);
                                }
                            }
                            
                            ClearChartPoints(chart1);
                        }
                        
                        if(nextProduct)
                        {
                            ClearTable();
                            nextProduct = false;
                        }
                        
                        if(startTestTime == 0)
                        {
                            startTestTime = DateTime.Now.Second;
                        }
                        
                        //modbus-rtu通信协议时的处理
                        byte[] torsion1 = rtuCom.ReadHoldReg(1, 5, 1);
                        
                        if(torsion1 != null)
                        {
                            if(torsion1.Length >= 2)
                            {
                                datY = (float)((torsion1[0] << 8 | torsion1[1]) / 9.54);//9.67
                            }
                            
                            SetChart(chart1, new PointF(datX, datY));
                            datX++;
                            //modbus-tcp通信协议时的处理
                            //if(dianpi1.Connected)
                            //{
                            //    dianpi1.ReadDataByFc03(51, 1);
                            //}
                            //if(dianpi2.Connected)
                            //{
                            //    dianpi2.ReadDataByFc03(51, 1);
                            //}
                        }
                    }
                    else if(completeOneScrewFlag)
                    {
                        if(!nextPoint)
                        {
                            AddOneRowData(datY);
                            datX = 0;   //新的曲线需重新设置横坐标起始位置
                            nextPoint = true;   //本点的曲线给完，准备下一个点的曲线的绘制
                            listBox1.Items.Add("打完一个螺丝");
                            listBox1.SelectedIndex = listBox1.Items.Count - 1;
                        }
                    }
                    else if(completeOneProductFlag && isStart)
                    {
                        if(!nextProduct)
                        {
                            nextProduct = true;
                            listBox1.Items.Add("完成一个产品");
                            listBox1.SelectedIndex = listBox1.Items.Count - 1;
                        }
                    }
                    
                    //调试模拟测试信号
                    //CurveDataTest();
                }
            }
        }
        
        /// <summary>
        /// 扭力曲线调试用
        /// </summary>
        private void CurveDataTest()
        {
            PointF point;
            Random ran = new Random();
            int RandKey = ran.Next(2, 4);
            datY = (float)1.23 * RandKey;
            point = new PointF(datX, datY);
            //points.Add(point);
            SetChart(chart1, point);
            
            if(startTestTime == 0)
            {
                startTestTime = DateTime.Now.Second;
            }
            
            if(datX++ >= 200)
            {
                AddOneRowData(datY);
                datX = 0;   //模拟采集200个点数据
            }
        }
        
        /// <summary>
        /// 添加一行有效数据
        /// </summary>
        /// <param name="torsion"></param>
        private void AddOneRowData(float torsion)
        {
            int tableRowNum = dataGridView1.RowCount;   //添加数据前先获取表格中现有的行数
            int dataRowNum = torsions.Count;    //获取表格中实际有数据的行数，因为开始时都是空白行
            string passState = string.Empty;
            Color cellColor;
            
            if(torsion >= torsionDown && torsion <= torsionUp)
            {
                passNum++;
                passState = "Pass";
                cellColor = Color.Green;
            }
            else
            {
                failNum++;
                passState = "Fail";
                cellColor = Color.Red;
            }
            
            totalScrewNum++;
            testNum.Text = totalScrewNum.ToString();
            passRate = (float)(passNum * 100) / (float)(totalScrewNum);
            straitRate.Text = string.Format("{0}%", passRate.ToString("f2"));
            endTestTime = DateTime.Now.Second;
            testTime.Text = (endTestTime - startTestTime).ToString();
            startTestTime = 0;
            int pointIndex = dataRowNum + 1; ;
            
            if(dataRowNum < tableRowNum)
            {
                //当表格中实际数据行数小于表格总行数时，更新下一行现有内容，填充空白内容
                dataGridView1.Rows[dataRowNum].Cells[0].Value = string.Format("点{0}", pointIndex);
                dataGridView1.Rows[dataRowNum].Cells[1].Value = torsion;
                dataGridView1.Rows[dataRowNum].Cells[2].Value = torsionUp;
                dataGridView1.Rows[dataRowNum].Cells[3].Value = torsionDown;
                dataGridView1.Rows[dataRowNum].Cells[4].Value = passState;
                dataGridView1.Rows[dataRowNum].Cells[4].Style.BackColor = cellColor;
            }
            else
            {
                //添加新的数据行方法一
                DataRow newRow = mDt.NewRow();
                newRow[pointCol] = string.Format("点{0}", pointIndex);
                newRow[niuliCol] = torsion;
                newRow[upLimitCol] = torsionUp;
                newRow[downLimitCol] = torsionDown;
                newRow[resultCol] = passState;
                mDt.Rows.Add(newRow);
                dataGridView1.Rows[dataRowNum - 1].Cells[4].Style.BackColor = cellColor;
                //添加新的数据行方法二(在绑定数据源的情况下不可行)
                //int index = dataGridView1.Rows.Add();
                //dataGridView1.Rows[index].Cells[0].Value = string.Format("点{0}", dataRowNum);
                //dataGridView1.Rows[index].Cells[1].Value = torsion;
                //dataGridView1.Rows[index].Cells[2].Value = torsionUp;
                //dataGridView1.Rows[index].Cells[3].Value = torsionDown;
                //dataGridView1.Rows[index].Cells[4].Value = passState;
                //方法三
                //DataGridViewRow row = new DataGridViewRow();
                //DataGridViewTextBoxCell textboxcell = new DataGridViewTextBoxCell();
                //textboxcell.Value = "aaa";
                //row.Cells.Add(textboxcell);
                //DataGridViewComboBoxCell comboxcell = new DataGridViewComboBoxCell();
                //row.Cells.Add(comboxcell);
                //dataGridView1.Rows.Add(row);
            }
            
            string curTorsionStr = string.Format("点位{0}的完成扭力为：{1}", pointIndex, datY.ToString(/*"f4"*/));
            listBox1.Items.Add(curTorsionStr);
            listBox1.SelectedIndex = listBox1.Items.Count - 1;
            torsions.Add(torsion);  //将新的数据添加到扭力数据链表中去
        }
        
        /// <summary>
        /// 根据新的数据进行曲线绘制
        /// </summary>
        /// <param name="chart"></param>
        /// <param name="point"></param>
        /// <param name="drawType">0:静态曲线绘制; 1:动态曲线绘制;</param>
        public void SetChart(Chart chart, PointF point, bool drawType = false)
        {
            if(drawType)
            {
                //数据量到达指定个数时横坐标每加一个数据左移一个点
                chart.Series[0].Points.AddXY(point.X, point.Y);
                
                //曲线整体向左平移一个点位
                if(chart.Series[0].Points.Count > curveXnum)
                {
                    chart.ChartAreas[0].AxisX.ScaleView.Position = chart.Series[0].Points.Count - curveXnum;
                }
            }
            //else
            //{
            //    //固定坐标,新来点的曲线覆盖原曲线的方法
            //    if (chart.Series[0].Points.Count > curveXnum)
            //    {
            //        ClearChartPoints(chart);
            //    }
            //    else
            //    {
            //        // 添加数据
            //        chart.Series[0].Points.AddXY(point.X, point.Y);
            //    }
            //}
            else
            {
                //固定坐标,新来点的曲线覆盖原曲线的方法
                if(chart.Series[0].Points.Count > curveXnum)
                {
                    int temp = curveXnum;
                    curveXnum *= 2;
                    xInterval *= 2;
                    chart.ChartAreas[0].AxisX.ScaleView.Size = curveXnum;
                    chart.ChartAreas[0].AxisX.Interval = xInterval;
                    hasCoordXChanged = true;
                    
                    for(int i = temp; i <= curveXnum; i++)
                    {
                        chart1.Series[1].Points.AddXY(i, torsionUp);
                        chart1.Series[2].Points.AddXY(i, torsionDown);
                    }
                }
                
                // 添加数据
                chart.Series[0].Points.AddXY(point.X, point.Y);
            }
        }
        
        /// <summary>
        /// 清空原曲线的数据并断开绘制
        /// </summary>
        /// <param name="chart"></param>
        private void ClearChartPoints(Chart chart)
        {
            chart.Series[0].Points.Clear();
            chart.Series[0].Points.AddXY(0, 0); //防止没有数据源时重绘坐标轴
            chart.Series[0].Points[0].IsEmpty = true;//不连接
        }
        
        private static int failWriteCnt = 0;    //写入失败计数
        private static int writeCnt = 1;   //切换产品时总是有写入失败的情况发生，先定义2次连续写入看看有没有好转
        private void WritePointsToPLC()
        {
            string curTime = DateTime.Now.ToString("yyyy-MM-dd  hh:mm:ss");//获取格式化的当前时间
            SingleRDpData onePos;
            onePos.x = 0f;
            onePos.y = 0f;
            onePos.z = 0f;
            onePos.u = 0f;
            //onePos.z2 = 0;
            onePos.dp = 0;
            string msg = string.Empty;
            
            //int pe = 0;
            if(curPointList.Count > 0 && curSendPoint < 30)
            {
                if(curSendPoint < curPointList.Count)
                {
                    string[] pointUnit = curPointList[curSendPoint].Split(',');
                    onePos.x = Convert.ToSingle(pointUnit[0]);
                    onePos.y = Convert.ToSingle(pointUnit[1]);
                    onePos.z = Convert.ToSingle(pointUnit[2]);
                    
                    if(Product.CurProduct == Product.ProductEnum.Product001)
                    {
                        onePos.u = Convert.ToSingle(pointUnit[3]);
                        onePos.dp = Convert.ToInt32(pointUnit[4]);
                    }
                    else if(Product.CurProduct == Product.ProductEnum.Product002)
                    {
                        onePos.dp = Convert.ToInt32(pointUnit[3]);
                    }
                    
                    //onePos.z2 = Convert.ToSingle(pointUnit[4]);
                    //onePos.pe = Convert.ToInt32(pointUnit[6]);
                    msg = string.Format("{0} - 向PLC写入点{1}: {2}", curTime, curSendPoint + 1, curPointList[curSendPoint]);
                }
                else
                {
                    onePos.x = 0;
                    onePos.y = 0;
                    onePos.z = 0;
                    onePos.u = 0;
                    //onePos.z2 = 0;
                    onePos.dp = 0;
                    
                    //onePos.pe = 0;
                    if(Product.CurProduct == Product.ProductEnum.Product001)
                    {
                        msg = string.Format("{0} - 向PLC写入点{1}: {2},{3},{4},{5},{6}", curTime, curSendPoint + 1, onePos.x, onePos.y, onePos.z, onePos.u, onePos.dp);
                    }
                    else if(Product.CurProduct == Product.ProductEnum.Product002)
                    {
                        msg = string.Format("{0} - 向PLC写入点{1}: {2},{3},{4},{5}", curTime, curSendPoint + 1, onePos.x, onePos.y, onePos.z, onePos.dp);
                    }
                }
                
                if(writeCnt == 0)
                {
                    listBox1.Items.Add("参数写入中，请稍候...");
                    listBox1.SelectedIndex = listBox1.Items.Count - 1;
                }
                
                if(!comm.WriteDMDat(addrX1 + 2 * curSendPoint, onePos.x))
                {
                    failWriteCnt++;
                }
                
                if(!comm.WriteDMDat(addrZ1 + 2 * curSendPoint, onePos.y))
                {
                    failWriteCnt++;
                }
                
                if(!comm.WriteDMDat(addrY + 2 * curSendPoint, onePos.z))
                {
                    failWriteCnt++;
                }
                
                if(Product.CurProduct == Product.ProductEnum.Product001)
                {
                    if(!comm.WriteDMDat(addrX2 + 2 * curSendPoint, onePos.u))
                    {
                        failWriteCnt++;
                    }
                }
                
                if(!comm.WriteDMDat(addrZ2 + 2 * curSendPoint, onePos.dp))
                {
                    failWriteCnt++;
                }
                
                if(writeCnt == 1)
                {
                    listBox1.Items.Add(msg);
                    listBox1.SelectedIndex = listBox1.Items.Count - 1;
                }
                
                curSendPoint++;
                
                if(curSendPoint >= 30)
                {
                    failWriteCnt = 0;
                    curSendPoint = 0;
                    writeCnt++;
                    
                    if(writeCnt >= 2)
                    {
                        writeCnt = 0;
                        sendToPlcFlag = false;  //发送完点位信息后就没必要再发送点位信息了
                        readStatusFlag = true;  //发送完点位信息后就要使能状态查询了
                        listBox1.Items.Add("参数写入完成！");
                        listBox1.Items.Add(string.Format("一共有{0}个参数写入失败", failWriteCnt));
                        listBox1.SelectedIndex = listBox1.Items.Count - 1;
                    }
                }
            }
        }
        
        private void WritePointToPLC()
        {
            string curTime = DateTime.Now.ToString("yyyy-MM-dd  hh:mm:ss");//获取格式化的当前时间
            
            if(curPointList.Count > 0)
            {
                string[] pointUnit = curPointList[curSendPoint].Split(',');
                SingleRDpData onePos;
                onePos.x = Convert.ToSingle(pointUnit[0]);
                onePos.y = Convert.ToSingle(pointUnit[1]);
                onePos.z = Convert.ToSingle(pointUnit[2]);
                onePos.u = 0f;
                
                if(Product.CurProduct == Product.ProductEnum.Product001)
                {
                    onePos.u = Convert.ToSingle(pointUnit[3]);
                }
                
                //float z2 = Convert.ToSingle(pointUnit[4]);
                onePos.dp = Convert.ToInt32(pointUnit[4]);
                //int pe = Convert.ToInt32(pointUnit[6]);
                
                switch(curCoordCmd)
                {
                    case WriteCoordCmd.None:
                    case WriteCoordCmd.CoordX:
                        comm.WriteDMData(addrX1 + 2 * curSendPoint, onePos.x);
                        curCoordCmd = WriteCoordCmd.CoordY;
                        break;
                        
                    case WriteCoordCmd.CoordY:
                        comm.WriteDMData(addrZ1 + 2 * curSendPoint, onePos.y);
                        curCoordCmd = WriteCoordCmd.CoordZ;
                        break;
                        
                    case WriteCoordCmd.CoordZ:
                        comm.WriteDMData(addrY + 2 * curSendPoint, onePos.z);
                        
                        if(Product.CurProduct == Product.ProductEnum.Product001)
                        {
                            curCoordCmd = WriteCoordCmd.CoordU;
                        }
                        else if(Product.CurProduct == Product.ProductEnum.Product002)
                        {
                            curCoordCmd = WriteCoordCmd.CoordD;
                            addrZ2 = addrX2;
                        }
                        
                        break;
                        
                    case WriteCoordCmd.CoordU:
                        comm.WriteDMData(addrX2 + 2 * curSendPoint, onePos.u);
                        curCoordCmd = WriteCoordCmd.CoordD;
                        break;
                        
                    case WriteCoordCmd.CoordD:
                        comm.WriteDMData(addrZ2 + 2 * curSendPoint, onePos.dp);
                        string msg = string.Format("{0} - 向PLC写入点{1}: {2}", curTime, curSendPoint + 1, curPointList[curSendPoint]);
                        listBox1.Items.Add(msg);
                        listBox1.SelectedIndex = listBox1.Items.Count - 1;
                        curCoordCmd = WriteCoordCmd.None;
                        curSendPoint++;
                        break;
                        
                    default:
                        break;
                }
            }
            
            if(curSendPoint >= curPointList.Count)
            {
                curSendPoint = 0;
                sendToPlcFlag = false;  //发送完点位信息后就没必要再发送点位信息了
                readStatusFlag = true;  //发送完点位信息后就要使能状态查询了
            }
        }
        /// <summary>
        /// 发送延时函数
        /// </summary>
        /// <param name="ms"></param>
        private void Delayms(int ms)
        {
            if(ms > 0)
            {
                for(int i = 0; i < ms; i++)
                {
                    for(int j = 0; j < 1000; j++)
                    {
                    }
                }
            }
        }
        /// <summary>
        /// 获取当前产品的点位信息
        /// </summary>
        /// <param name="product"></param>
        /// <returns></returns>
        private List<string> GetPointInfoByProduct(string product)
        {
            //加载当前产品的点位信息
            List<string> pList = new List<string>();
            pList = iniFileOp.ReadValues(product);
            return pList;
        }
        /// <summary>
        /// 根据最新数据设置更新图表曲线
        /// </summary>
        /// <param name="chart"></param>
        /// <param name="plist"></param>
        public void SetChart(Chart chart, List<PointF> plist)
        {
            chart.Series[0].Points.Clear();
            
            // 添加数据
            for(int i = 0; i < plist.Count; i++)
            {
                chart.Series[0].Points.AddXY(plist[i].X, plist[i].Y);
                
                //曲线整体向左平移一个点位
                if(i > 8)   //8为chart.ChartAreas[0].AxisX.ScaleView.size.
                {
                    chart.ChartAreas[0].AxisX.ScaleView.Position = plist.Count - 8;//chart.Series[0].Points.Count - 1;
                }
            }
        }
        /// <summary>
        /// 改变ListBox2中的项的颜色
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBox2_DrawItem(object sender, DrawItemEventArgs e)
        {
            if(e.Index >= 0)
            {
                e.DrawBackground();
                Brush errBrush = Brushes.Red;
                e.DrawFocusRectangle();
                e.Graphics.DrawString(listBox2.Items[e.Index].ToString(), e.Font, errBrush, e.Bounds, StringFormat.GenericDefault);
            }
        }
    }
}
