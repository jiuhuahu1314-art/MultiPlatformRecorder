using System;
using System.Threading.Tasks;
using BililiveRecorder.Core.Config.V3;
using BililiveRecorder.Core.Templating;
using Newtonsoft.Json.Linq;
using Serilog;

#nullable enable
namespace BililiveRecorder.WPF.Pages
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage
    {
        private static readonly ILogger logger = Log.ForContext<SettingsPage>();

        private static readonly FileNameTemplateContext data = new()
        {
            Name = "测试直播间",
            RoomId = 1234567,
            ShortId = 0,
            Uid = 7654321,
            Title = "测试直播标题",
            AreaParent = "测试分区",
            AreaChild = "测试子分区",
            Qn = 10000,
            Json = JObject.Parse(@"{""room_info"":{""uid"":7654321,""room_id"":1234567,""short_id"":0,""title"":""测试直播标题"",""cover"":"""",""tags"":"""",""background"":"""",""description"":""测试直播间描述"",""live_status"":1,""live_start_time"":0,""live_screen_type"":0,""lock_status"":0,""lock_time"":0,""hidden_status"":0,""hidden_time"":0,""area_id"":0,""area_name"":""测试子分区"",""parent_area_id"":0,""parent_area_name"":""测试分区"",""keyframe"":"""",""special_type"":0,""up_session"":"""",""pk_status"":0,""is_studio"":false,""pendants"":{""frame"":{""name"":"""",""value"":"""",""desc"":""""}},""on_voice_join"":0,""online"":0,""room_type"":{}},""anchor_info"":{""base_info"":{""uname"":""测试主播"",""face"":"""",""gender"":""保密"",""official_info"":{""role"":0,""title"":"""",""desc"":"""",""is_nft"":0,""nft_dmark"":""""}},""live_info"":{""level"":0,""level_color"":0,""score"":0,""upgrade_score"":0,""current"":[],""next"":[],""rank"":""""},""relation_info"":{""attention"":0},""medal_info"":{""medal_name"":"""",""medal_id"":0,""fansclub"":0}}}")
        };

        private readonly GlobalConfig? globalConfig;

        public SettingsPage() : this((GlobalConfig?)(RootPage.ServiceProvider?.GetService(typeof(GlobalConfig))))
        {
        }

        public SettingsPage(GlobalConfig? globalConfig)
        {
            this.globalConfig = globalConfig;

            this.InitializeComponent();
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void TestFileNameTemplate_Button_Click(object sender, System.Windows.RoutedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (this.globalConfig is not { } config)
                return;

            try
            {
                var output = await Task.Run(() =>
                {
                    var fileNameGenerator = new FileNameGenerator(config, null);
                    return fileNameGenerator.CreateFilePath(data);
                });

                this.FileNameTestResultArea.Visibility = System.Windows.Visibility.Visible;
                this.FileNameTestResultArea.DataContext = output;
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "测试文件名模板时发生错误");
            }
        }
    }
}
