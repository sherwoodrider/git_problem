using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using TuyaUpdate.Properties;

namespace TuyaUpdate
{
    public partial class SftUpdate : Form
    {
        #region 定义变量
        BackgroundWorker Worker1 = new BackgroundWorker();
        public int progressPercent = 0;         //进度百分比值0--100(%)
        /// <summary>
        /// 软件更新下载成功标志
        /// </summary>
        bool isFirmDwOK;
        /// <summary>
        /// 获取软件版本
        /// </summary>
        public string SftVersion { get; set; }//配置软件版本

        /// <summary>
        /// 获取软件ID
        /// </summary>
        public string SftIdentity { get; set; }
        /// <summary>
        /// 获取软件名称
        /// </summary>
        public string SftName { get; set; } = "DefaultName";//这里用json里面的替代，以解决多语言问题

        bool HasNewVersion;
        string UpgradeCnDesc = "";//升级文案(英文)
        string UpdateWay = "";//app_force_upgrade=APP强制升级 ，  app_remind_upgrade=APP提醒升级， app_check_upgrade=APP检测升级
        string UpVersion = "";
        string Url, Md5;
        /// <summary>
        /// 上传并获取数据成功标志
        /// </summary>
        bool LoadSuccess = false;

        public enum _LoginUrlType
        {
            Release = 0, //线上
            Preview = 1, //预发
            Daily = 2,   //日常
        };

        public _LoginUrlType ConfigLoginType { get; set; } = _LoginUrlType.Release; //配置域名类型

        /// <summary>
        /// 获取域名
        /// </summary>
        public string LoginUrl
        {
            get
            {
                switch (ConfigLoginType)
                {
                    case _LoginUrlType.Release:
                        return "https://a1.tuyacn.com/pt.json?";
                    case _LoginUrlType.Preview:
                        return "http://a.gw.cn.wgine.com/pt.json?";
                    case _LoginUrlType.Daily:
                        return "https://a.daily.tuya-inc.cn/pt.json?";
                    default:
                        return "https://a1.tuyacn.com/pt.json?";//默认线上
                }
            }
        }
        #endregion
        /// <summary>
        /// 软件版本更新
        /// </summary>
        /// <param name="sftVersion">软件版本</param>
        /// <param name="sftIdentity">软件ID</param>
        /// <param name="sftName">软件名称</param>
        /// <param name="type">获取域名方式</param>
        public SftUpdate(string sftVersion, string sftIdentity,string sftName,_LoginUrlType type)
        {
            SftIdentity = sftIdentity;
            SftName = sftName;
            SftVersion = sftVersion;
            ConfigLoginType = type;
            InitializeComponent();
        }
        /// <summary>
        /// 更新工具版本号
        /// </summary>
        /// <returns></returns>
        public static string GetVersion()
        {
            return "1.0.0";
        }
        /// <summary>
        /// 设定Default名字和路径
        /// </summary>
        private void SetSettings()
        {
            try
            {
                Settings.Default.SftName = SftName;
                Settings.Default.Msipath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData) + @"\Tuya\" + Settings.Default.SftName;
                //如果文件夹不存在，重新生成
                if (!Directory.Exists(Settings.Default.Msipath))
                    Directory.CreateDirectory(Settings.Default.Msipath);
            }
            catch (Exception ew)
            {
                MessageBox.Show(ew.ToString());
            }
        }
        /// <summary>
        /// 检查更新方式
        /// </summary>
        private void checkForUpgrade()
        {
            if (HasNewVersion)
            {
                Settings.Default.upgradeCnDesc = UpgradeCnDesc;
                Settings.Default.versionText = "当前版本：" + SftVersion + "有更新版本：" + UpVersion;
                if (UpdateWay == "app_force_upgrade")
                {
                    Upgrade();
                }
                else if (UpdateWay == "app_remind_upgrade" || UpdateWay == "app_check_upgrade")//目前提醒与检测一样
                {
                    ConfirmUpgrade c = new ConfirmUpgrade();
                    c.ShowDialog();
                    if (Settings.Default.needUpgrade)
                    {
                        Upgrade();
                    }
                }
                else 
                {
                    MessageBox.Show("更新方式未定义！");
                }
            }
            else
            {
                WorkerReportProgress(100, "当前已是最新版本！");
            }
        }
        /// <summary>
        /// 上传到服务端检查是否有更新
        /// </summary>
        /// <returns></returns>
        private bool WikiCkeckUpgrade()
        {
            bool result = false;
            SoftwareVerUpgradeReqParas req = new SoftwareVerUpgradeReqParas();
            SoftwareVerUpgradeRspParas rsp = new SoftwareVerUpgradeRspParas();
            req.softwareIdentity = SftIdentity;
            req.softwareVersion = SftVersion;
            rsp = SoftwareVersionUpgrade(req);
            if (rsp.success == true)
            {
                HasNewVersion = rsp.result.hasNewVersion;
                if (rsp.result.hasNewVersion)
                {
                    UpdateWay = rsp.result.productionSoftwareVersionVO.upgradeWay;
                    UpgradeCnDesc = rsp.result.productionSoftwareVersionVO.upgradeCnDesc;
                    UpVersion = rsp.result.productionSoftwareVersionVO.version;
                    Md5 = rsp.result.fileList[0].md5;
                    Url = rsp.result.fileList[0].fullUrl;
                }
                else
                {

                }
                result = true;
                LoadSuccess = true;
            }
            else
            {
                MessageBox.Show("获取返回值失败，请检查网络设置！","提示：",MessageBoxButtons.OK,MessageBoxIcon.Error);
                //if (rsp.errorMsg != null)
                //{
                //    MessageBox.Show(rsp.errorMsg);
                //    //WorkerReportProgress(50, "获取返回值失败!");
                //}
                //else
                //{
                //    MessageBox.Show("获取返回值失败！");
                //}

            }
            return result;
        }
        #region 新增封装方法

        public class BaseRequest
        {
            public string ToJsonString()
            {
                try
                {
                    string str = JsonConvert.SerializeObject(this);
                    return str;
                }
                catch
                {
                    return "";
                }

            }

        }
        public class BaseResponse
        {
            public bool success;
            public string errorMsg;
            public string errorCode;
            public string status;
            public long t;
            public string ToJsonString()
            {
                try
                {
                    string str = JsonConvert.SerializeObject(this);
                    return str;
                }
                catch
                {
                    return "";
                }

            }

        }

        //软件版本检查
        public class SoftwareVerUpgradeReqParas : BaseRequest
        {
            public string softwareIdentity;
            public string softwareVersion;
        }

        public class SoftwareVerUpgradeRspParas : BaseResponse
        {
            public SoftwareVerUpgradeResult result;
        }
        public class SoftwareVerUpgradeResult
        {
            public bool hasNewVersion;
            public ProductionSoftwareVersionVO productionSoftwareVersionVO = new ProductionSoftwareVersionVO();
            public List<FileInfo> fileList = new List<FileInfo>();
        }
        public class ProductionSoftwareVersionVO
        {
            public string description; //软件说明
            public string remark; //备注
            public string isDeleted;
            public string modifier;
            public string creator;
            public int softwareId;
            public long gmtModified;
            public string upgradeEnDesc; //升级文案(英文)
            public string upgradeWay; //app_force_upgrade=APP强制升级 ，  app_remind_upgrade=APP提醒升级， app_check_upgrade=APP检测升级
            public string upgradeCnDesc; //升级文案(中文)
            public int status;
            public int id;
            public string version;//软件版本
            public string principal;
            public long gmtCreate;
        }
        public class FileInfo
        {
            public string md5;
            public string name;
            public long gmtModified;
            public string size;
            public string fullUrl;//文件下载地址
            public string bizType;// 软件安装包=software_installation,  升级安装包=software_upgrade
            public int id;
            public string url;
            public long gmtCreate;
            public string bizId;
        }
        #endregion
        public SoftwareVerUpgradeRspParas SoftwareVersionUpgrade(SoftwareVerUpgradeReqParas ReqParas, string apiVer = "1.0")
        {
            SoftwareVerUpgradeRspParas RspParas = new SoftwareVerUpgradeRspParas();
            try
            {
                string Data = JsonConvert.SerializeObject(ReqParas);
                string strRet = TuyaCloudIfLib.TuyaCloudIf.GetValue(
                    LoginUrl, SftVersion, 
                    "s.pt.software.version.upgrade.check",Data
                    );

                RspParas = JsonConvert.DeserializeObject<SoftwareVerUpgradeRspParas>(strRet);
                return RspParas;
            }
            catch (Exception ex)
            {
                RspParas.errorMsg = ex.Message;
                RspParas.success = false;
                return RspParas;
            }
        }
        private void Update_Load(object sender, EventArgs e)
        {
            if (SftIdentity != "" && SftVersion != "")
            {
                bool result = DllfileCheck();
                if (!result)
                {
                    // return;
                    this.Close();
                }
                else
                try
                {
                    BackgroundCheck();
                    return;
                }
                catch (Exception ew)
                {
                    MessageBox.Show("上传校验出错！");
                }
            }
            else
            {
                MessageBox.Show("软件版本或ID为空！");
            }
        }
        private bool DllfileCheck()
        {
            string  path = Application.StartupPath;
            if (!File.Exists(path + "\\TuyaCloudIfLib.dll"))
            {
                MessageBox.Show("缺少依赖项目：TuyaCloudIfLib.dll，请放到当前文件路径。", "提示：", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (!File.Exists(path + "\\Newtonsoft.Json.dll"))
            {
                MessageBox.Show("缺少依赖项目：Newtonsoft.Json.dll，请放到当前文件路径。", "提示：", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }
        /// <summary>
        /// 启动worker1，上传并获取返回值
        /// </summary>
        private void BackgroundCheck()
        {
            Worker1.WorkerReportsProgress = true;
            Worker1.WorkerSupportsCancellation = true;
            Worker1.DoWork += Worker1_DoWork;
            Worker1.ProgressChanged += Worker1_ProgressChanged;
            Worker1.RunWorkerCompleted += Worker1_RunWorkerCompleted;
            Worker1.RunWorkerAsync();
        }
        private void WorkerReportProgress(string text = "正在检查更新......")
        {
            progressPercent += 5;
            Worker1.ReportProgress(progressPercent,text);
        }
        private void WorkerReportProgress(int progressPercent, string text)
        {
            Worker1.ReportProgress(progressPercent, text);
        }
        private void Worker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int getPercentage = e.ProgressPercentage;
            progressBar1.Value = getPercentage;
            //if ((!HasNewVersion) && progressBar1.Value == 100&&LoadSuccess)
            //    lb_text.Text = "当前已是最新版本！";
            //else
            lb_text.Text = (string)e.UserState;
        }

        private void Worker1_DoWork(object sender, DoWorkEventArgs e)
        {
            SetSettings();
            for (int i = 0; i < 10; i++)
            {
                WorkerReportProgress();
                Thread.Sleep(30);
            }
            bool result = WikiCkeckUpgrade();
           
            if (!result)
            {
                WorkerReportProgress(100, "上传检查错误！");
                Thread.Sleep(500);
                return;
            }
            else
            {
                for (int i = 0; i < 10; i++)
                {
                    WorkerReportProgress();
                    Thread.Sleep(30);
                }
                checkForUpgrade();
                Thread.Sleep(500);
            }
        }
       
        private void Worker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Thread.Sleep(500);
            this.Close();
        }
        #region 下载更新

        /// <summary>
        /// 更新后台启动
        /// </summary>
        private void Upgrade()
        {
            if (Url == "" || Md5 == "")
            {
                WorkerReportProgress(0, "ERROR：传入值为空！");
                return;
            }
            WorkerReportProgress(0, "正在下载更新版本......");
            try
            {
                DownLoad();
                if (!isFirmDwOK)
                    return;
                //System.Diagnostics.Process.Start(Settings.Default.Msipath + @"\updateSetup.msi");//之前是下载完成直接打开，目前禁用
            }
            catch (Exception eq)
            {
                MessageBox.Show("文件下载失败！", "提示：", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }
        ///   <summary>
        ///   给一个File进行MD5加密
        ///   </summary>
        ///   <param   name="strText">待加密文件路径</param>
        ///   <returns>加密后的字符串</returns>
        private static string MD5EncryptFile(string path)
        {
            byte[] buffer = File.ReadAllBytes(path);

            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] result = md5.ComputeHash(buffer);
            string value = "";
            foreach (byte a in result)
            {
                value += a.ToString("x2");
            }
            return value;
        }
        /// <summary>
        /// 从取得的url下载文件
        /// </summary>
        /// <returns></returns>
        private bool DownLoad()
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);
            request.Proxy = null;
            HttpWebResponse response = request.GetResponse() as HttpWebResponse;
            Stream responseStream = response.GetResponseStream();
            long totalsize = response.ContentLength;
            long exsistsize = 0;
            Stream stream = new FileStream(Settings.Default.Msipath + @"\updateSetup.msi", FileMode.Create);
            byte[] bArr = new byte[1024];

            int size = responseStream.Read(bArr, 0, bArr.Length);
            exsistsize += size;
            int percent = 0;
            while (size > 0)
            {
                stream.Write(bArr, 0, size);
                size = responseStream.Read(bArr, 0, bArr.Length);
                exsistsize += size;
                percent = (int)(exsistsize * 100 / totalsize);
                WorkerReportProgress(percent, "正在下载更新版本......");
            }
            stream.Close();
            responseStream.Close();
            WorkerReportProgress(100, "下载完成！");

            string temp = MD5EncryptFile(Settings.Default.Msipath + @"\updateSetup.msi");

            if (temp != Md5)
            {
                WorkerReportProgress(100, "文件校验失败！重新下载...");
                isFirmDwOK = false;
            }
            else
            {
                WorkerReportProgress(100, "校验成功");
                isFirmDwOK = true;
            }

            Thread.Sleep(1000);
            return isFirmDwOK;
        }

        #endregion
        /// <summary>
        /// 软件更新版本是否下载成功
        /// </summary>
        /// <returns></returns>
        public bool GetDownloadResult()
        {
            return isFirmDwOK;
        }
    }
}
