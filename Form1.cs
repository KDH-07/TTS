using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;

namespace TTS
{
    public partial class Form1 : Form
    {
        public List<string> inti_local;
        string disaster_kind;
        string code;
        string inti;
        string inti_permo;
        string inti_max;
        string inti_result;
        string kind;
        string loc;
        string mt;
        string dep;
        string time;
        string des;
        string arrivaltime;
        string sitearrivaltime;
        string msg;
        string filePath = "pastkma.txt";
        string previousMessage = "";

        public Form1()
        {
            InitializeComponent();
        }


        public void local()
        {
            try
            {
                // 생성자에서 리스트 초기화
                inti_local = new List<string>();
                string url = "http://192.168.123.108:8080/api/sido.json";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 20000;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                if (response.StatusCode != HttpStatusCode.OK) return;

                // 인코딩
                Stream stream = response.GetResponseStream();
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                string text = reader.ReadToEnd();

                // json 파싱
                Console.WriteLine(text);
                JArray data = JArray.Parse(text);

                // 진도 정보 추출
                Dictionary<string, List<string>> mmiDict = new Dictionary<string, List<string>>();
                foreach (var item in data)
                {
                    string name = item["name"]?.ToString();
                    string mmi = item["mmi"]?.ToString();

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(mmi))
                    {
                        JArray sigungu = (JArray)item["sigungu"];
                        bool hasSameMmi = false;

                        foreach (var subItem in sigungu)
                        {
                            string subName = subItem["name"]?.ToString();
                            string subMmi = subItem["mmi"]?.ToString();

                            if (!string.IsNullOrEmpty(subName) && !string.IsNullOrEmpty(subMmi))
                            {
                                // 진도가 시/도와 같으면 플래그 설정
                                if (subMmi == mmi)
                                {
                                    hasSameMmi = true;
                                }

                                // 진도가 Ⅰ이면 건너뛰기
                                if (subMmi != "Ⅰ")
                                {
                                    if (!mmiDict.ContainsKey(subMmi))
                                    {
                                        mmiDict[subMmi] = new List<string>();
                                    }
                                    mmiDict[subMmi].Add($"{name} {subName}");
                                }
                            }
                        }

                        if (mmi != "Ⅰ" && !hasSameMmi)
                        {
                            if (!mmiDict.ContainsKey(mmi))
                            {
                                mmiDict[mmi] = new List<string>();
                            }
                            mmiDict[mmi].Add(name);
                        }
                    }
                }

                // 긴 데이터를 처리하는 로직
                foreach (var mmi in mmiDict.Keys.OrderByDescending(m => m))
                {
                    var dataList = mmiDict[mmi];
                    if (dataList.Count > 25) // 25개 이상의 항목이 있는 경우
                    {
                        inti_local.Add($"진도{mmi} 외 {dataList.Count}개 지역");
                    }
                    else
                    {
                        inti_local.Add($"진도{mmi} {string.Join(", ", dataList)}");
                    }
                }

                // inti_local이 비어있는 경우 과거 데이터를 불러오기
                if (inti_local.Count == 0 || inti_local.All(string.IsNullOrEmpty))
                {
                    if (File.Exists("pastmmi.txt"))
                    {
                        inti_local = File.ReadAllLines("pastmmi.txt").ToList();
                        File.WriteAllText("errormmi.txt", $"{DateTime.Now:yyyy-MM-dd hh:mm:ss} - nothing");
                    }
                }
                else
                {
                    File.WriteAllText("pastmmi.txt", string.Join("\n", inti_local));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);

                // 오류가 발생했을 때 과거 데이터를 불러오기
                if (File.Exists("pastmmi.txt"))
                {
                    inti_local = File.ReadAllLines("pastmmi.txt").ToList();
                }
            }
        }




        public void Info()
        {
            try
            {
                // 현재 메시지를 파일에 저장
                File.WriteAllText(filePath, msg);
                string url = "http://192.168.123.108:8080/api/kma.json";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 20000;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                if (response.StatusCode != HttpStatusCode.OK) return;

                // 인코딩
                Stream stream = response.GetResponseStream();
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                string text = reader.ReadToEnd();

                // json 파싱
                Console.WriteLine(text);
                JObject obj = JObject.Parse(text);

                // 재난종류 확인
                disaster_kind = (string)(obj["evt_kind"]);

                code = (string)(obj["eq_doce"]);
                loc = (string)(obj["eq_loc"]);
                des = (string)(obj["eq_des"]);
                kind = (string)(obj["eq_kind"]);
                time = (string)(obj["eq_time"]);

                // 지진이라면?
                if (disaster_kind == "Earthquake")
                {
                    mt = (string)(obj["eq_mt"]);
                    inti = (string)(obj["eq_inti"]);
                    dep = (string)(obj["eq_dep"]);



                    // 최대진도 가공 제공되는 것만
                    if (code == "102" || code == "103")
                    {
                        // 최대진도Ⅱ 이상은 데이터 가공이 필요함
                        if (inti != "최대진도 Ⅰ") /* 진도 Ⅰ은 무조건 최대진도 Ⅰ임 하드코딩*/
                        {
                            // "최대진도 "를 제거한 문자열 생성
                            string removedPrefix = inti.Replace("최대진도 ", "");

                            // 로마자 숫자만 따로 보관
                            string romanNumber = removedPrefix.Substring(0, removedPrefix.IndexOf("("));

                            // 괄호를 포함한 지역명 제거
                            string removedBracket = removedPrefix.Substring(removedPrefix.IndexOf("(") + 1);
                            removedBracket = removedBracket.Remove(removedBracket.IndexOf(")"));

                            // 뒤에 오는 문자 제거
                            string finalString = removedBracket.Split(' ')[0];

                            inti_max = romanNumber;


                            if (romanNumber == "Ⅱ")
                            {
                                inti_permo = "진도 Ⅱ의 진동은 조용한 상태나 건물위층에 있는 소수의 사람만이 느낄 수 있습니다.";
                            }
                            if (romanNumber == "Ⅲ")
                            {
                                inti_permo = "진도 Ⅲ의 진동은 실내, 특히 고층 건물에 있는 사람이 현저하게 느끼며 정지해 있는 차가 약간 흔들릴 수 있습니다.";
                            }
                            if (romanNumber == "Ⅳ")
                            {
                                inti_permo = "진도 Ⅳ의 진동은 실내에서 많은 사람이 느끼고, 밤에는 잠에서 깨기도 하며, 그릇과 창문 등이 흔들릴 수 있습니다.";
                            }
                            if (romanNumber == "Ⅴ")
                            {
                                inti_permo = "진도 Ⅴ의 진동은 거의 모든 사람이 진동을 느끼고, 그릇과 창문 등이 깨지기도 하며 불안한 물체가 넘어지는 피해가 발생할 수 있습니다.";
                            }
                            if (romanNumber == "Ⅵ")
                            {
                                inti_permo = "진도 Ⅵ의 진동은 실내에서 많은 사람이 느끼고, 밤에는 잠에서 깨기도 하며, 그릇과 창문 등이 흔들릴 수 있습니다.";
                            }
                            if (romanNumber == "Ⅶ")
                            {
                                inti_permo = "진도 Ⅶ의 진동은 일반 건물에 약간의 피해가 발생하며, 부실한 건물에는 상당한 피해가 발생할 수 있습니다.";
                            }
                            if (romanNumber == "Ⅷ")
                            {
                                inti_permo = "진도 Ⅷ의 진동은 일반 건물에 부분적 붕괴 등 상당한 피해가 발생하고, 부실한 건물에는 심각한 피해가 발생할 수 있습니다.";
                            }
                            if (romanNumber == "Ⅸ")
                            {
                                inti_permo = "진도 Ⅸ의 진동은 잘 설계된 건물에도 상당한 피해가 발생하며, 일반 건축물에는 붕괴 등 큰 피해가 발생할 수 있습니다.";
                            }
                            if (romanNumber == "Ⅹ")
                            {
                                inti_permo = "진도 Ⅹ의 진동은 남아있는 구조물이 거의 없으며, 다리가 무너지고, 기차선로가 심각하게 휘어질 수 있습니다.";
                            }

                            local();

                            // inti_local이 비어있는 경우 과거 데이터를 불러오기
                            if (inti_local.Count == 0 || inti_local.All(string.IsNullOrEmpty))
                            {
                                inti_local = File.ReadAllLines("pastmmi.txt").ToList();
                            }

                            Dictionary<string, string> romanNumberMap = new Dictionary<string, string>()
                            {
                                {"Ⅱ", "Ⅱ가"},
                                {"Ⅲ", "Ⅲ이"},
                                {"Ⅳ", "Ⅳ가"},
                                {"Ⅴ", "Ⅴ가"},
                                {"Ⅵ", "Ⅵ이"},
                                {"Ⅶ", "Ⅶ이"},
                                {"Ⅷ", "Ⅷ이"},
                                {"Ⅸ", "Ⅸ가"},
                                {"Ⅹ", "Ⅹ이"}
                            };

                            string kr = romanNumberMap[romanNumber];

                            inti_result = "이번 지진으로 인해 " + finalString + "에 최대진도" + romanNumber + "에 흔들림이 전달 됐습니다. " + inti_permo + " 지역별 진도는 다음과 같습니다.";


                            foreach (var info in inti_local)
                            {
                                if (info == null)
                                {
                                    errorlog.Text = "지역별 진도 값이 NULL입니다.";
                                }
                                else if (info == "")
                                {
                                    errorlog.Text = "지역별 진도 값이 비어있습니다.";
                                }
                                else
                                {
                                    inti_result += " " + info + " ";
                                    infobox.Text = info;
                                }
                            }
                        }

                        // 최대진도Ⅰ은 가공X (별도 지역명 처리)
                        if (inti == "최대진도 Ⅰ")
                        {
                            inti_permo = "진도 Ⅰ의 진동은 대부분 사람들은 느낄 수 없으나, 지진계에는 기록됩니다.";
                            // 하드코딩 값 사용
                            // 현재 데이터를 파일에 저장
                            File.WriteAllText("pastmmi.txt", string.Join("\n", "최대진도 Ⅰ"));
                            inti_result = "이번 지진으로 인해 " + "최대진도Ⅰ" + "에 흔들림이 전달 됐습니다. " + inti_permo;
                            infobox.Text = "최대진도 Ⅰ";
                        }
                    }

                    // 지진정보
                    if (code == "102")
                    {
                        if (dep == "-")
                        {
                            msg = "기상청 발표 지진상세정보를 전해드립니다. " + "조금 전 " + time + "에 " + loc + "에서 규모 " + mt + "의 지진이 발생했습니다." + inti_result + "참고사항은 다음과 같습니다. " + des;
                        }
                        else
                        {
                            msg = "기상청 발표 지진상세정보를 전해드립니다. " + "조금 전 " + time + "에 " + loc + "에서 규모 " + mt + "의 지진이 발생했습니다." + " 진원의 깊이는 " + dep + "km로 분석됐습니다. " + inti_result + "참고사항은 다음과 같습니다. " + des;
                        }

                    }
                    // 지진정보 (재통보)
                    if (code == "103")
                    {
                        if (dep == "-")
                        {
                            msg = "기상청 발표 재통보 지진상세정보를 전해드립니다. " + "조금 전 " + time + "에 " + loc + "에서 규모 " + mt + "의 지진이 발생했습니다." + inti_result + "참고사항은 다음과 같습니다. " + des;
                        }
                        else
                        {
                            msg = "기상청 발표 재통보 지진상세정보를 전해드립니다. " + "조금 전 " + time + "에 " + loc + "에서 규모 " + mt + "의 지진이 발생했습니다." + " 진원의 깊이는 " + dep + "km로 분석됐습니다. " + inti_result + "참고사항은 다음과 같습니다. " + des;
                        }
                    }
                    // 국외지진정보
                    if (code == "104")
                    {
                        if (dep == "-")
                        {
                            msg = "기상청 발표 국외지진정보를 전해드립니다. " + "조금 전 " + time + "에 " + loc + "에서 규모 " + mt + "의 지진이 발생했습니다." + " 참고사항입니다. " + des;
                        }
                        else
                        {
                            msg = "기상청 발표 국외지진정보를 전해드립니다. " + "조금 전 " + time + "에 " + loc + "에서 규모 " + mt + "의 지진이 발생했습니다." + " 진원의 깊이는 " + dep + "km로 분석됐습니다." + " 참고사항입니다. " + des;
                        }
                    }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Info();
        }

        // 처음 시작하는지 확인하는 플래그 추가
        private bool isFirstRun = true;

        private void timer1_Tick(object sender, EventArgs e)
        {
            // 파일이 존재하는지 확인
            if (File.Exists(filePath))
            {
                // 파일이 존재하면 파일에서 이전 메시지 읽기
                previousMessage = File.ReadAllText(filePath);
            }
            else
            {
                // 파일이 없으면 쓰기
                string savePath = @"pastkma.txt";
                System.IO.File.WriteAllText(savePath, msg, Encoding.Default);
            }

            if (previousMessage != msg)
            {
                msgbox.Text = msg;

                if (!isFirstRun)
                {
                    if(inti != "최대진도 Ⅰ"){
                    if (code == "102")
                    {
                        if (inti_local == null || !inti_local.Any())
                        {
                            errorlog.Text = "지역별 진도 값이 NULL이거나 비어있습니다.";
                        }
                        else
                        {
                            foreach (var info in inti_local)
                            {
                                if (string.IsNullOrEmpty(info))
                                {
                                    errorlog.Text = "지역별 진도 값이 NULL이거나 비어있습니다.";
                                }
                                // inti_max 값이 inti_local에 포함되어 있는지 확인
                                if (info.Contains(inti_max))
                                {
                                    infobox.Text = info;
                                    tts.LoadUrl("https://playentry.org/api/expansionBlock/tts/read.mp3?text=" + msg + "&speed=0&pitch=0&speaker=jinho&volume=5");
                                }
                            }
                        }
                      }
                    }

                    if (inti == "최대진도 Ⅰ")
                    {
                        if (code == "102")
                        {
                            infobox.Text = "최대진도 Ⅰ";
                            tts.LoadUrl("https://playentry.org/api/expansionBlock/tts/read.mp3?text=" + msg + "&speed=0&pitch=0&speaker=jinho&volume=5");
                        }
                    }
                        
                    
                    if (code == "104")
                    {
                        infobox.Text = "미제공";
                        tts.LoadUrl("https://playentry.org/api/expansionBlock/tts/read.mp3?text=" + msg + "&speed=0&pitch=0&speaker=jinho&volume=5");
                    
                    }
                }
                else
                {
                    isFirstRun = false;
                }
            }

            // 독립적으로 처리하도록 함수 호출 위치 변경
            Info();

        }
    }
}