using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightChase.Station
{
    public sealed class ArchiveCabinet : StationInteractable
    {
        const string HeroSurname = "Карамазов";
        const string CaseNumber = "94-17";
        static readonly Color Ink = new(.16f,.14f,.11f);
        static readonly Color RuleInk = new(.47f,.42f,.33f);
        static Texture2D paperTexture;
        static readonly string[] CasePages =
        {
            "Отчет детектива Миллера. 10 августа 1999 г.\n" +
            "При осмотре склада №4 в Бруклинском порту обнаружены мешки с нитратом аммония. Следы взрывателя.\n" +
            "Свидетель (охранник) показал: «Видел мужчину, славянская внешность, назвался \"инспектором\". Сказал, что проверяет груз для судоходной компании».\n" +
            "Фотография с камеры наблюдения прилагается.",
            "Подозрительно: на складе найдена визитка с номером, который принадлежит фирме «Славянский экспорт» — подставной компании, связанной с бывшими сотрудниками КГБ, осевшими в Бруклине в 1990 году."
        };
        [SerializeField] AudioClip witnessRecording;
        AudioSource tape;
        static readonly string[] OtherNames = { "Волков", "Громов", "Зайцев", "Ковалёв", "Лебедев", "Орлов", "Соколов" };

        public override string Prompt(Scene2FlowManager flow) => "E — открыть архивный шкаф";

        public override void Use(Scene2FlowManager flow)
        {
            flow.Lock(true);
            ShowCabinet(flow);
        }

        void ShowCabinet(Scene2FlowManager flow)
        {
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Архивный шкаф",flow.ClosePanel);
            ui.Label(screen,$"Найдите ячейку «{HeroSurname}».",new(.05f,.79f),new(.95f,.87f),26);
            for (int i = 0; i < 6; i++)
            {
                string name = i == 2 ? HeroSurname : OtherNames[i < 2 ? i : i - 1];
                float x = .07f + i % 3 * .30f;
                float y = .51f - i / 3 * .26f;
                ui.Button(screen,name,new(x,y),new(x + .25f,y + .19f),() => ShowDrawer(flow,name));
            }
        }

        void ShowDrawer(Scene2FlowManager flow, string surname)
        {
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Ячейка · " + surname,() => ShowCabinet(flow),"К шкафу");
            flow.Audio.PlayOneShot(flow.Audio.paper);
            if (surname == HeroSurname)
            {
                ui.Button(screen,$"Дело отца · № {CaseNumber}",new(.12f,.52f),new(.88f,.69f),() => Read(flow));
                ui.Button(screen,"Кассета · Показания свидетеля",new(.12f,.36f),new(.88f,.48f),() => Listen(flow));
                ui.Button(screen,"Кассета 44-Б · Запись отца",new(.12f,.20f),new(.88f,.32f),() => Listen44B(flow));
                ui.Label(screen,"В деле отца два листа.",new(.12f,.07f),new(.88f,.16f),26);
            }
            else ui.Label(screen,"В этой ячейке нет нужного дела.",new(.12f,.3f),new(.88f,.62f),30);
        }

        void Listen44B(Scene2FlowManager flow)
        {
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Кассета 44-Б",() => ShowDrawer(flow,HeroSurname),"К папкам");
            var notes = InvestigationNotes.For(flow);
            ui.Label(screen,"Расшифровка\n\n«Кравец сказал, что всё готово к пятнадцатому. Я спросил про слушание. Он велел мне больше не звонить».",
                new(.11f,.32f),new(.89f,.81f),30);
            ui.Button(screen,"Отметить расшифровку прочитанной",new(.11f,.13f),new(.59f,.24f),() =>
            {
                notes.Tape44BHeard = true;
                ui.Label(screen,"Содержание кассеты отмечено в журнале.",new(.11f,.02f),new(.89f,.10f),21);
            });
            var recording = Resources.Load<AudioClip>("StationEvidence/Tape44B");
            if (!recording) return;
            if (!tape) tape = gameObject.AddComponent<AudioSource>();
            tape.clip = recording;
            var transport = EvidenceWorkspace.Rect(screen,"Кассета 44-Б",Vector2.zero,Vector2.one);
            transport.gameObject.AddComponent<StopArchiveTape>().Tape = tape;
            ui.Button(transport,"Прослушать",new(.64f,.13f),new(.90f,.24f),() =>
            {
                tape.Play();
                notes.Tape44BHeard = true;
            });
        }

        void Listen(Scene2FlowManager flow)
        {
            var clip = witnessRecording ? witnessRecording : Resources.Load<AudioClip>("StationEvidence/WitnessRecording");
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Показания свидетеля",() => ShowDrawer(flow,HeroSurname),"К папкам");
            var transcript = ui.Label(screen,"",new(.11f,.35f),new(.89f,.79f),30);
            transcript.text = "Расшифровка · 00:12\n\n«После девяти дверь ещё открывалась. Я вышла через северный вход, когда закончилась смена».";
            var notes = InvestigationNotes.For(flow);
            ui.Button(screen,notes.TapePinned ? "Фрагмент закреплён" : "Закрепить фрагмент · 00:12",
                new(.11f,.24f),new(.6f,.34f),() =>
                {
                    notes.TapePinned = true;
                    Listen(flow);
                });
            if (!clip)
            {
                ui.Label(screen,"Временная расшифровка. Аудиофайл пока не назначен.",new(.11f,.12f),new(.89f,.23f),22);
                return;
            }
            if (!tape) tape = gameObject.AddComponent<AudioSource>();
            tape.playOnAwake = false;
            tape.clip = clip;
            var transport = EvidenceWorkspace.Rect(screen,"Кассета",Vector2.zero,Vector2.one);
            transport.gameObject.AddComponent<StopArchiveTape>().Tape = tape;
            bool paused = false;
            ui.Button(transport,"Играть / пауза",new(.11f,.12f),new(.37f,.23f),() =>
            {
                if (tape.isPlaying) { tape.Pause(); paused = true; }
                else if (paused) { tape.UnPause(); paused = false; }
                else tape.Play();

            });
            ui.Button(transport,"−10 секунд",new(.39f,.12f),new(.61f,.23f),() => Seek(-10));
            ui.Button(transport,"+10 секунд",new(.63f,.12f),new(.89f,.23f),() => Seek(10));
        }

        void Seek(float delta)
        {
            tape.time = Mathf.Clamp(tape.time + delta,0,Mathf.Max(0,tape.clip.length - .05f));
        }

        void Read(Scene2FlowManager flow)
        {
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Дело отца",() => ShowDrawer(flow,HeroSurname),"К папкам  [Esc]");
            flow.Audio.PlayOneShot(flow.Audio.paper);
            var frame = EvidenceWorkspace.Rect(screen,"Case sheet area",new(.045f,.035f),new(.955f,.865f));
            var edge = EvidenceWorkspace.Rect(frame,"Case paper edge",Vector2.zero,Vector2.one);
            var aspect = edge.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = .79f;
            PrintRule(edge,"Paper shadow",new(.005f,-.012f),new(1.024f,.982f),new(.028f,.031f,.032f));
            PrintRule(edge,"Paper edge",Vector2.zero,Vector2.one,new(.53f,.48f,.39f));
            var sheet = EvidenceWorkspace.Rect(edge,"Case paper",new(.005f,.004f),new(.995f,.996f));
            if (!paperTexture) paperTexture = Resources.Load<Texture2D>("StationEvidence/ArchivePaper");
            var paper = sheet.gameObject.AddComponent<RawImage>();
            paper.texture = paperTexture;
            paper.color = paperTexture ? new Color(.97f,.99f,1f) : new Color(.89f,.85f,.76f);
            paper.raycastTarget = false;
            PrintRule(sheet,"Binding margin",new(0,0),new(.069f,1),new(.30f,.25f,.17f,.12f));
            PrintRule(sheet,"Binding fold",new(.068f,0),new(.071f,1),new(.34f,.29f,.21f,.33f));
            PrintRule(sheet,"Top double rule",new(.118f,.938f),new(.92f,.9405f),RuleInk);
            PrintRule(sheet,"Top rule",new(.118f,.928f),new(.92f,.9295f),RuleInk);
            PrintRule(sheet,"Header rule",new(.118f,.78f),new(.92f,.782f),RuleInk);
            PrintRule(sheet,"Footer rule",new(.118f,.145f),new(.92f,.147f),RuleInk);

            var heading = ui.Label(sheet,$"ДЕЛО № {CaseNumber}",new(.118f,.796f),new(.92f,.914f),40);
            heading.alignment = TextAlignmentOptions.Center;
            heading.fontStyle = FontStyles.Bold;
            heading.characterSpacing = 6;
            heading.color = Ink;

            var content = EvidenceWorkspace.Rect(sheet,"Page content",Vector2.zero,Vector2.one);
            var page = content.gameObject.AddComponent<CanvasGroup>();
            var body = ui.Label(content,"",new(.118f,.174f),new(.92f,.749f),22);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.color = Ink;
            body.fontSizeMin = 16;
            body.characterSpacing = 2;
            body.wordSpacing = 4;
            body.lineSpacing = 4;
            body.paragraphSpacing = 42;

            var folio = ui.Label(content,"",new(.118f,.047f),new(.92f,.112f),17);
            folio.color = RuleInk;
            Button previous = null, next = null;
            previous = PaperButton("← Предыдущая страница", .118f, .70f, 0);
            next = PaperButton("Следующая страница →", .338f, .92f, 1);
            ShowPage(0);

            Button PaperButton(string title, float minX, float maxX, int target)
            {
                var button = ui.Button(content,title,new(minX,.047f),new(maxX,.112f),() => Turn(target));
                button.image.color = new Color(.37f,.31f,.22f,.11f);
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = label.fontSizeMax = 19;
                label.characterSpacing = 1;
                label.color = Ink;
                return button;
            }

            void ShowPage(int index)
            {
                body.text = CasePages[index];
                folio.text = $"{index + 1} / {CasePages.Length}";
                folio.alignment = index == 0 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
                previous.gameObject.SetActive(index == 1);
                next.gameObject.SetActive(index == 0);
                ui.Left = index == 1 ? () => Turn(0) : null;
                ui.Right = index == 0 ? () => Turn(1) : null;
            }

            void Turn(int target)
            {
                if (!page || !page.gameObject.activeInHierarchy || !page.interactable) return;
                page.interactable = false;
                flow.Audio.PlayOneShot(flow.Audio.paper);
                StartCoroutine(FadePage(page,() => ShowPage(target)));
            }
        }

        static void PrintRule(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var image = EvidenceWorkspace.Rect(parent,name,min,max).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static IEnumerator FadePage(CanvasGroup page, System.Action changePage)
        {
            const float duration = .14f;
            for (float time = 0; time < duration; time += Time.unscaledDeltaTime)
            {
                if (!page || !page.gameObject.activeInHierarchy) yield break;
                page.alpha = 1 - Mathf.SmoothStep(0,1,time / duration);
                yield return null;
            }
            if (!page || !page.gameObject.activeInHierarchy) yield break;
            page.alpha = 0;
            changePage();
            for (float time = 0; time < duration; time += Time.unscaledDeltaTime)
            {
                if (!page || !page.gameObject.activeInHierarchy) yield break;
                page.alpha = Mathf.SmoothStep(0,1,time / duration);
                yield return null;
            }
            if (!page || !page.gameObject.activeInHierarchy) yield break;
            page.alpha = 1;
            page.interactable = true;
        }
    }
}
