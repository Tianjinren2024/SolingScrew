using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace SolingScrew.DataDal
{
    class ModbusRtuDev
    {
        /// <summary>
        /// Modbus功能码枚举
        /// </summary>
        public enum ModbusCmd
        {
            CmdCode03,  //读保持寄存器
            CmdCode06,  //写单个寄存器
            CmdCode10,  //写多个寄存器
        }
        /// <summary>
        /// 串口操作错误码
        /// </summary>
        public enum ModbusError
        {
            Normal,     //正常
            NoDev,      //没有串口设备
            OpenFailed, //串口打开失败
            CloseFailed,//串口关闭失败
        }
        /// <summary>
        /// 校验位枚举
        /// </summary>
        public enum CheckType
        {
            None,       //无校验
            Odd,        //奇校验
            Even,       //偶校验
        }
        /// <summary>
        /// 命令部分属性
        /// </summary>
        public struct CmdProperty
        {
            public int devId;       //设备地址
            public ModbusCmd cmd;   //命令功能码
            public int addr;        //命令要操作的寄存器首地址
            public int len;         //命令要操作的寄存器个数
        }
        
        public struct SerialPortProperty
        {
            public string serialPortName;   //串口名称
            public int baudRate;            //串口所用波特率
            public int startBit;            //起始位
            public int dataBit;             //数据位
            public int stopBit;             //停止位
            public int checkBit;            //校验位
        }
        
        #region 常量定义
        private byte funCode03 = 0x03;      //读保持寄存器
        private byte funCode06 = 0x06;      //写单寄存器
        private byte funCode10 = 0x10;      //写多寄存器
        //private const byte readCoils = 0x01;              //读线圈寄存器
        //private const byte readDiscrete = 0x02;           //读离散输入寄存器
        //private const byte readHoldReg = 0x03;            //读保持寄存器
        //private const byte readInputReg = 0x04;           //读输入寄存器
        //private const byte writeSingleCoil = 0x05;        //写单个线圈
        //private const byte writeSingleReg = 0x06;         //写单寄存器
        //private const byte writeMultipleCoils = 0x0f;     //写多线圈
        //private const byte writeMultipleRegs = 0x10;      //写多寄存器
        #endregion
        
        #region 状态变量区
        private bool isOpen = false;//指定串口是否打开的状态变量
        private ModbusError errorCode = ModbusError.Normal;
        #endregion
        
        #region 变量区
        private SerialPort serialPort = null;     //声明一个串口实例
        private SerialPortProperty serialConfig = new SerialPortProperty();
        #endregion
        
        #region 事件声明区
        public delegate void DataReceivedHandler(byte[] bytes);
        public event DataReceivedHandler DataReceive;
        #endregion
        
        #region 公有变量区
        public bool IsOpen
        {
            get
            {
                return isOpen;
            }
            set
            {
                isOpen = value;
            }
        }
        #endregion
        
        
        #region 单例模式定义区
        private static ModbusRtuDev rtuInstance = null;
        private ModbusRtuDev() { }
        public static ModbusRtuDev GetRtuInstance()
        {
            if(rtuInstance == null)
            {
                rtuInstance = new ModbusRtuDev();
            }
            
            return rtuInstance;
        }
        #endregion
        
        
        /// <summary>
        /// 配置串口属性
        /// </summary>
        #region 方法区
        public void InitSerialPort()
        {
            if(ScanSerialPort())
            {
                serialPort = new SerialPort();
                SerialConfig();
                OpenSerialPort();
            }
        }
        public void InitSerialPort(int baud)
        {
            if(ScanSerialPort())
            {
                serialPort = new SerialPort();
                SerialConfig(baud);
                OpenSerialPort();
            }
        }
        private void SerialConfig()
        {
            serialPort.PortName = "COM2";   //默认使用串口2，串口1被PLC使用了
            serialPort.BaudRate = 19200;
            serialPort.DataBits = 8;
            serialPort.Parity = Parity.None;
            serialPort.StopBits = StopBits.One;
            //serialPort.ReadTimeout = -1;  //超时读取时间的设置
            //serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceived);
        }
        private void SerialConfig(int baudRate)
        {
            serialPort.PortName = "COM2";   //默认使用串口2，串口1被PLC使用了
            serialPort.BaudRate = baudRate;
            serialPort.DataBits = 8;
            serialPort.Parity = Parity.None;
            serialPort.StopBits = StopBits.One;
            //serialPort.ReadTimeout = -1;  //超时读取时间的设置
            //serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceived);
        }
        private void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int itemp = serialPort.BytesToRead;
            byte[] datas = new byte[itemp];       //开辟接收缓冲区
            serialPort.Read(datas, 0, datas.Length);  //从串口读取数据
            
            if(datas.Length > 0)
            {
                DataReceive(datas);
            }
        }
        /// <summary>
        /// 配置串口属性
        /// </summary>
        private void SerialConfig(string comName, int baudRate, int startBit, int dataBit, StopBits stopBit, Parity parity)
        {
            serialPort.PortName = comName;
            serialPort.BaudRate = baudRate;
            serialPort.DataBits = dataBit;
            serialPort.Parity = parity;
            serialPort.StopBits = stopBit;
            //scom.ReadTimeout = -1;  //超时读取时间的设置
            //serialPort.DataReceived += new SerialDataReceivedEventHandler(ScomDataReceived);
        }
        /// <summary>
        /// 扫描是是否存在串口设备
        /// </summary>
        /// <returns></returns>
        private bool ScanSerialPort()
        {
            bool res = false;
            
            if(SerialPort.GetPortNames().Count() > 1)
            {
                res = true;
            }
            
            return res;
        }
        
        /// <summary>
        /// 打开串口
        /// </summary>
        public void OpenSerialPort()
        {
            try
            {
                serialPort.Open();
                IsOpen = serialPort.IsOpen;
            }
            catch(Exception)
            {
                errorCode = ModbusError.OpenFailed;
            }
        }
        /// <summary>
        /// 关闭串口
        /// </summary>
        public void CloseSerialPort()
        {
            try
            {
                serialPort.Close();
                IsOpen = serialPort.IsOpen;
            }
            catch(Exception)
            {
                errorCode = ModbusError.CloseFailed;
            }
        }
        /// <summary>
        /// 读取保持寄存器的值
        /// </summary>
        /// <param name="devId"></param>
        /// <param name="addr"></param>
        /// <param name="regLen"></param>
        /// <returns></returns>
        public int ReadHoldReg03(int devId, int addr, int regLen)
        {
            int res = 0;
            byte[] cmd = FormatCmdToSend(devId, 3, addr, regLen, null);
            res = SendData(cmd);
            return res;
        }
        /// <summary>
        /// 写单个寄存器
        /// </summary>
        /// <param name="devId"></param>
        /// <param name="addr"></param>
        /// <param name="regLen"></param>
        /// <param name="dat"></param>
        /// <returns></returns>
        public int WriteSingleReg06(int devId, int addr, int regLen, byte[] dat)
        {
            int res = 0;
            byte[] cmd = FormatCmdToSend(devId, 6, addr, regLen, dat);
            res = SendData(cmd);
            return res;
        }
        /// <summary>
        /// 写多个寄存器
        /// </summary>
        /// <param name="devId"></param>
        /// <param name="addr"></param>
        /// <param name="regLen"></param>
        /// <param name="dat"></param>
        /// <returns></returns>
        public int WriteMultipleReg10(int devId, int addr, int regLen, byte[] dat)
        {
            int res = 0;
            byte[] cmd = FormatCmdToSend(devId, 16, addr, regLen, dat);
            res = SendData(cmd);
            return res;
        }
        /// <summary>
        /// 发送字节数据
        /// </summary>
        /// <param name="bytes"></param>
        private int SendData(byte[] cmdByte)
        {
            int res = 0;
            
            if(cmdByte == null)
            {
                return -1;
            }
            
            try
            {
                serialPort.Write(cmdByte, 0, cmdByte.Length);   //成功返回0
            }
            catch(System.Exception ex)
            {
                res = -1;   //失败返回-1
            }
            
            return res;
        }
        
        /// <summary>
        /// 收发同步的处理
        /// </summary>
        /// <param name="cmdByte"></param>
        /// <returns></returns>
        private byte[] SendAndReceive(byte[] cmdByte)
        {
            if(IsOpen)
            {
                try
                {
                    serialPort.Write(cmdByte, 0, cmdByte.Length);   //成功返回0
                    int timeOut = 0;    //超时计数初值,防止收不到数据进入死循环
                    
                    while(serialPort.BytesToRead == 0 && timeOut < 10000)
                    {
                        Thread.Sleep(1);    //延时1ms确保收到数据
                        timeOut++;  //超时计数
                    }
                    
                    Thread.Sleep(10);   //延时10ms确保收到数据
                    byte[] receiveByte = new byte[serialPort.BytesToRead];
                    int length = serialPort.Read(receiveByte, 0, serialPort.BytesToRead);
                    byte[] receive = null;
                    
                    if(receiveByte.Length > 2)
                    {
                        receive = new byte[receiveByte.Length - 2];
                        bool res = ValidateFrameByCrc16(receiveByte, out receive);  //接收数据有效性判断
                        
                        if(res)
                        {
                            return receive; //有效则返回接收到的数据
                        }
                        else
                        {
                            return null;    //无效则丢弃返回空
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
                catch
                {
                    return null;
                    // MessageBox.Show("接收数据失败！");
                }
            }
            
            return null;
        }
        /// <summary>
        /// 读保持寄存器
        /// </summary>
        /// <param name="devId"></param>
        /// <param name="addr"></param>
        /// <param name="regLen"></param>
        /// <returns></returns>
        public byte[] ReadHoldReg(int devId, int addr, int regLen)
        {
            byte[] cmd = FormatCmdToSend(devId, 3, addr, regLen, null);
            byte[] dat = SendAndReceive(AssemblyFrameByCrc16(cmd));
            byte[] res = DecodeReceiveData(cmd, dat);
            return res;
        }
        
        /// <summary>
        /// 写单个寄存器
        /// </summary>
        /// <param name="devId"></param>
        /// <param name="addr"></param>
        /// <param name="regLen"></param>
        /// <param name="dat"></param>
        /// <returns></returns>
        public byte[] WriteSingleReg(int devId, int addr, int regLen, byte[] dat)
        {
            byte[] cmd = FormatCmdToSend(devId, 6, addr, regLen, dat);
            return SendAndReceive(AssemblyFrameByCrc16(cmd));
        }
        
        /// <summary>
        /// 写多个寄存器
        /// </summary>
        /// <param name="devId"></param>
        /// <param name="addr"></param>
        /// <param name="regLen"></param>
        /// <param name="dat"></param>
        /// <returns></returns>
        public byte[] WriteMultipleReg(int devId, int addr, int regLen, byte[] dat)
        {
            byte[] cmd = FormatCmdToSend(devId, 16, addr, regLen, dat);
            return SendAndReceive(AssemblyFrameByCrc16(cmd));
        }
        /// <summary>
        /// 根据发送数据与接收数据对接收到的数据进行解析
        /// </summary>
        /// <param name="send"></param>
        /// <param name="receive"></param>
        /// <returns></returns>
        private byte[] DecodeReceiveData(byte[] send, byte[] receive)
        {
            byte[] resdat = null;
            
            if(receive != null)
            {
                int dev = 0;    //收到的设备地址
                int code = 0;
                
                if(receive.Length > 3)   //收到数据的基本长度，若小于这个数则说明接收有问题而不往下走了
                {
                    dev = Convert.ToInt32(receive[0]);
                    code = Convert.ToInt32(receive[1]);
                    
                    if(send[0] == receive[0] && send[1] == receive[1])   //匹配设备地址及功能码
                    {
                        try
                        {
                            switch(code)
                            {
                                case 3:
                                    int recLen = Convert.ToInt32(receive[2]);
                                    
                                    if(recLen == receive.Length - 3)
                                    {
                                        resdat = new byte[recLen];
                                        
                                        for(int i = 0; i < receive.Length; i++)
                                        {
                                            resdat[i] = receive[3 + i]; //返回的数据内容
                                        }
                                    }
                                    
                                    break;
                                    
                                case 6:
                                case 16:
                                    if(receive.Length == 6)
                                    {
                                        resdat = new byte[1];   //保存写入的结果
                                        resdat[0] = 1;
                                        
                                        for(int i = 0; i < 4; i++)
                                        {
                                            if(send[2 + i] != receive[2 + i])
                                            {
                                                resdat[0] = 0;  //发现一个不同就表示写入失败
                                            }
                                        }
                                    }
                                    
                                    break;
                                    
                                default:
                                    break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            
            return resdat;
        }
        private byte[] DecodeReceiveData(int devId, int funCode, int addr, int regLen, byte[] dat)
        {
            byte[] resdat = null;
            int dev = 0;    //收到的设备地址
            int code = 0;
            
            if(dat.Length > 3)
            {
                dev = Convert.ToInt32(dat[0]);
                code = Convert.ToInt32(dat[1]);
                
                if(dev == devId)
                {
                    if(code == funCode)
                    {
                        try
                        {
                            switch(code)
                            {
                                case 3:
                                    int recLen = Convert.ToInt32(dat[2]);
                                    
                                    if(recLen == dat.Length - 3)
                                    {
                                        resdat = new byte[recLen];
                                        
                                        for(int i = 0; i < dat.Length; i++)
                                        {
                                            resdat[i] = dat[3 + i]; //返回的数据内容
                                        }
                                    }
                                    
                                    break;
                                    
                                case 6:
                                    break;
                                    
                                case 16:
                                    break;
                                    
                                default:
                                    break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            
            return resdat;
        }
        
        /// <summary>
        /// 根据所传命令参数进行命令组合
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private byte[] FormatCmdToSend(int devId, int cmdCode, int addr, int regLen, byte[] dat)
        {
            int cmdByteLen = 6;//不同的功能码，它们的命令发送长度是固定的或可计算的
            int dataByteNum = 2 * regLen;
            
            if(cmdCode == 6 || cmdCode == 16)
            {
                cmdByteLen = 7 + dataByteNum;
            }
            
            byte[] cmdByte = new byte[cmdByteLen];
            cmdByte[0] = Convert.ToByte(devId);
            cmdByte[1] = Convert.ToByte(cmdCode);
            cmdByte[2] = (byte)((addr >> 8) & 0xff);
            cmdByte[3] = (byte)(addr & 0xff);
            cmdByte[4] = (byte)((regLen >> 8) & 0xff);
            cmdByte[5] = (byte)(regLen & 0xff);
            //cmdByte[2] = BitConverter.GetBytes(addr)[1];
            //cmdByte[3] = BitConverter.GetBytes(addr)[0];
            //cmdByte[4] = BitConverter.GetBytes(regLen)[1];
            //cmdByte[5] = BitConverter.GetBytes(regLen)[0];
            
            if(cmdCode == 6 || cmdCode == 16)
            {
                cmdByte[6] = Convert.ToByte(dataByteNum);
                
                for(int i = 0; i < dat.Length; i++)
                {
                    cmdByte[7 + i] = dat[i];
                }
            }
            
            return cmdByte;
        }
        
        
        #endregion
        
        
        #region 校验
        private readonly byte[] AuchCRCHi =
        {
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40
        };
        private readonly byte[] AuchCRCLo =
        {
            0x00, 0xC0, 0xC1, 0x01, 0xC3, 0x03, 0x02, 0xC2, 0xC6, 0x06, 0x07, 0xC7,
            0x05, 0xC5, 0xC4, 0x04, 0xCC, 0x0C, 0x0D, 0xCD, 0x0F, 0xCF, 0xCE, 0x0E,
            0x0A, 0xCA, 0xCB, 0x0B, 0xC9, 0x09, 0x08, 0xC8, 0xD8, 0x18, 0x19, 0xD9,
            0x1B, 0xDB, 0xDA, 0x1A, 0x1E, 0xDE, 0xDF, 0x1F, 0xDD, 0x1D, 0x1C, 0xDC,
            0x14, 0xD4, 0xD5, 0x15, 0xD7, 0x17, 0x16, 0xD6, 0xD2, 0x12, 0x13, 0xD3,
            0x11, 0xD1, 0xD0, 0x10, 0xF0, 0x30, 0x31, 0xF1, 0x33, 0xF3, 0xF2, 0x32,
            0x36, 0xF6, 0xF7, 0x37, 0xF5, 0x35, 0x34, 0xF4, 0x3C, 0xFC, 0xFD, 0x3D,
            0xFF, 0x3F, 0x3E, 0xFE, 0xFA, 0x3A, 0x3B, 0xFB, 0x39, 0xF9, 0xF8, 0x38,
            0x28, 0xE8, 0xE9, 0x29, 0xEB, 0x2B, 0x2A, 0xEA, 0xEE, 0x2E, 0x2F, 0xEF,
            0x2D, 0xED, 0xEC, 0x2C, 0xE4, 0x24, 0x25, 0xE5, 0x27, 0xE7, 0xE6, 0x26,
            0x22, 0xE2, 0xE3, 0x23, 0xE1, 0x21, 0x20, 0xE0, 0xA0, 0x60, 0x61, 0xA1,
            0x63, 0xA3, 0xA2, 0x62, 0x66, 0xA6, 0xA7, 0x67, 0xA5, 0x65, 0x64, 0xA4,
            0x6C, 0xAC, 0xAD, 0x6D, 0xAF, 0x6F, 0x6E, 0xAE, 0xAA, 0x6A, 0x6B, 0xAB,
            0x69, 0xA9, 0xA8, 0x68, 0x78, 0xB8, 0xB9, 0x79, 0xBB, 0x7B, 0x7A, 0xBA,
            0xBE, 0x7E, 0x7F, 0xBF, 0x7D, 0xBD, 0xBC, 0x7C, 0xB4, 0x74, 0x75, 0xB5,
            0x77, 0xB7, 0xB6, 0x76, 0x72, 0xB2, 0xB3, 0x73, 0xB1, 0x71, 0x70, 0xB0,
            0x50, 0x90, 0x91, 0x51, 0x93, 0x53, 0x52, 0x92, 0x96, 0x56, 0x57, 0x97,
            0x55, 0x95, 0x94, 0x54, 0x9C, 0x5C, 0x5D, 0x9D, 0x5F, 0x9F, 0x9E, 0x5E,
            0x5A, 0x9A, 0x9B, 0x5B, 0x99, 0x59, 0x58, 0x98, 0x88, 0x48, 0x49, 0x89,
            0x4B, 0x8B, 0x8A, 0x4A, 0x4E, 0x8E, 0x8F, 0x4F, 0x8D, 0x4D, 0x4C, 0x8C,
            0x44, 0x84, 0x85, 0x45, 0x87, 0x47, 0x46, 0x86, 0x82, 0x42, 0x43, 0x83,
            0x41, 0x81, 0x80, 0x40
        };
        
        /// <summary>
        /// 计算CRC16校验值
        /// </summary>
        /// <param name="buffer">输入的字节数组</param>
        /// <param name="crcHI">返回CRC16高位</param>
        /// <param name="crcLO">返回CRC16低位</param>
        /// <returns>返回ushort类型的CRC16校验值</returns>
        private byte[] CalculateCrc16(byte[] buffer)
        {
            byte crcHI = 0xff;  // high crc byte initialized
            byte crcLO = 0xff;  // low crc byte initialized
            
            if(buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            
            for(int i = 0; i < buffer.Length; i++)
            {
                int crcIndex = crcHI ^ buffer[i]; //查找crc表值
                crcHI = (byte)(crcLO ^ AuchCRCHi[crcIndex]);
                crcLO = AuchCRCLo[crcIndex];
            }
            
            return new byte[] { crcHI, crcLO };
        }
        
        /// <summary>
        /// 将CRC16校验值直接装配到传入的字节数组尾部
        /// </summary>
        /// <param name="buffer">传入的字节数组</param>
        /// <returns>装配了CRC16校验值后的字节数组</returns>
        /// <exception cref="System.ArgumentNullException">传入的字节数组为空</exception>
        /// <exception cref="System.ArgumentException">传入的字节数组长度小于等于0</exception>
        private byte[] AssemblyFrameByCrc16(byte[] buffer)
        {
            if(buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            
            byte[] crcBytes = CalculateCrc16(buffer);
            byte[] res = new byte[buffer.Length + crcBytes.Length];
            buffer.CopyTo(res, 0);
            crcBytes.CopyTo(res, buffer.Length);
            return res;
        }
        
        /// <summary>
        /// 对传入的带CRC16校验的数据帧进行校验，数据帧的最后两位为CRC16校验字节
        /// </summary>
        /// <param name="frame">带CRC16校验的数据帧</param>
        /// <param name="buffer">返回不带CRC16校验的字节数组</param>
        /// <returns>true:数据正确 false:数据错误</returns>
        /// <exception cref="System.ArgumentNullException">传入的字节数组为空</exception>
        /// <exception cref="System.ArgumentException">传入的字节数组长度小于等于2</exception>
        private bool ValidateFrameByCrc16(byte[] frame, out byte[] buffer)
        {
            if(frame == null)
            {
                throw new ArgumentNullException("frame");
            }
            
            if(frame.Length <= 2)
            {
                throw new ArgumentOutOfRangeException("frame", "传入的字节数组长度必须大于2");
            }
            
            byte[] tmpBuffer = new byte[frame.Length - 2];
            Array.Copy(frame, 0, tmpBuffer, 0, frame.Length - 2);
            byte[] crcBytes = CalculateCrc16(tmpBuffer);
            
            if(frame[frame.Length - 2] == crcBytes[0] && frame[frame.Length - 1] == crcBytes[1])
            {
                buffer = tmpBuffer;
                return true;
            }
            else
            {
                buffer = tmpBuffer;
                return false;
            }
        }
        
        /// <summary>
        /// 在输入的字节数组末端加上校验和字节,校验和算法为从头到尾异或
        /// </summary>
        /// <param name="inputValue">输入的字节数组</param>
        /// <returns>附加了校验和的字节数组</returns>
        private byte[] AssemblyFrameByCheckSum(byte[] inputValue)
        {
            if(inputValue == null)
            {
                throw new ArgumentNullException("inputValue", "inputBytes不能为Null");
            }
            
            long length = inputValue.Length;
            byte[] checkSum = new byte[] { 0 };
            
            for(int i = 0; i < length; i++)
            {
                checkSum[0] ^= inputValue[i];
            }
            
            return AppendArrays(inputValue, checkSum);
        }
        
        /// <summary>
        /// 验证输入的字节数组尾端的校验和是否正确
        /// </summary>
        /// <param name="frame">输入的字符数组</param>
        /// <param name="buffer">去掉校验和的字符数组</param>
        /// <returns>true:正确;false:错误</returns>
        public bool ValidateFrameByCheckSum(byte[] frame, out byte[] buffer)
        {
            if(frame == null)
            {
                throw new ArgumentNullException("frame");
            }
            
            if(frame.Length <= 1)
            {
                throw new ArgumentOutOfRangeException("frame", "传入的字节数组长度必须大于1");
            }
            
            byte[] tmpBuffer = SubArrays(frame, 0, frame.Length - 1);
            byte[] tmpFrame = AssemblyFrameByCheckSum(tmpBuffer);
            buffer = tmpBuffer;
            return EqualArrays(tmpFrame, frame);
        }
        
        private byte[] AppendArrays(byte[] src1, byte[] src2)
        {
            byte[] tmp = new byte[src1.Length + src2.Length];
            System.Buffer.BlockCopy(src1, 0, tmp, 0, src1.Length);
            System.Buffer.BlockCopy(src2, 0, tmp, src1.Length, src2.Length);
            return tmp;
        }
        /// <summary>
        /// 截取数组内指定范围的数据并形成一个新的数组
        /// </summary>
        /// <param name="src"></param>
        /// <param name="start"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        private byte[] SubArrays(byte[] src, int start, int len)
        {
            byte[] tmp = new byte[len];
            System.Buffer.BlockCopy(src, start, tmp, 0, len);
            return tmp;
        }
        
        /// <summary>
        /// 比较两个数组是否相等
        /// </summary>
        /// <param name="src1"></param>
        /// <param name="src2"></param>
        /// <returns></returns>
        private bool EqualArrays(byte[] src1, byte[] src2)
        {
            bool res = true;
            
            if(src1.Length == src2.Length)
            {
                for(int i = 0; i < src1.Length; i++)
                {
                    if(src1[i] != src2[i])
                    {
                        res = false;
                        return false;
                    }
                }
            }
            else
            {
                res = false;
            }
            
            return res;
        }
        #endregion
    }
}
