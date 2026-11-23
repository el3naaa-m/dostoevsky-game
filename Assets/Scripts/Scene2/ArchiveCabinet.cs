using UnityEngine;

namespace NightChase.Station
{
    public sealed class ArchiveCabinet : StationInteractable
    {
        [SerializeField] string heroSurname = "Достоевский";
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
            ui.Label(screen,"Найдите ячейку со своей фамилией. На удостоверении: Достоевский.",new(.05f,.79f),new(.95f,.87f),26);
            for (int i = 0; i < 6; i++)
            {
                string name = i == 2 ? heroSurname : OtherNames[i < 2 ? i : i - 1];
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
            if (surname == heroSurname)
            {
                ui.Button(screen,"Дело отца · № 94-17",new(.12f,.52f),new(.88f,.69f),() => Read(flow,0));
                ui.Button(screen,"Кассета · Показания свидетеля",new(.12f,.36f),new(.88f,.48f),() => Listen(flow));
                ui.Button(screen,"Кассета 44-Б · Запись отца",new(.12f,.20f),new(.88f,.32f),() => Listen44B(flow));
            }
            else ui.Label(screen,"В этой ячейке нет нужного дела.",new(.12f,.3f),new(.88f,.62f),30);
        }

        void Listen44B(Scene2FlowManager flow)
        {
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Кассета 44-Б",() => ShowDrawer(flow,heroSurname),"К папкам");
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
            var screen = ui.Page("Показания свидетеля",() => ShowDrawer(flow,heroSurname),"К папкам");
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

        void Read(Scene2FlowManager flow, int page)
        {
            var pages = new[]
            {
                "БЮРО · ДЕЛО № 94-17\n\nКарточка заведения дела. Октябрь 1994 года.\n\nОснование: запрос о несоответствии даты первого въезда.",
                "КРАТКИЙ ОТЧЁТ\n\nДежурный проверил двери после окончания смены.\nВ журнале под этой записью нет подписи.",
                "ЛИСТ ПОВТОРНОЙ ПРОВЕРКИ\n\nЗапрос в архив закрыт. Подпись в графе согласования отсутствует.\n\nНа обороте карандашом: «Брайтон-Бич. Служебный вход»."
            };
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Дело отца",() => ShowDrawer(flow,heroSurname),"К папкам");
            flow.Audio.PlayOneShot(flow.Audio.paper);
            var sheet = EvidenceWorkspace.Rect(screen,"Paper",new(.17f,.16f),new(.83f,.85f));
            sheet.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.9f,.85f,.72f);
            ui.Label(sheet,pages[page],new(.07f,.08f),new(.94f,.92f),32).color = new Color(.13f,.12f,.10f);
            var notes = InvestigationNotes.For(flow);
            if (page == 1)
                ui.Button(sheet, notes.Pinned ? InvestigationNotes.Excerpt + "  ·  Уже закреплено" : InvestigationNotes.Excerpt + "  ·  Закрепить на доске",
                    new(.055f,.09f),new(.945f,.28f),() =>
                    {
                        notes.Pinned = true;
                        Read(flow,page);
                    });
            ui.Left = page > 0 ? () => Read(flow,page - 1) : null;
            ui.Right = page + 1 < pages.Length ? () => Read(flow,page + 1) : null;
            ui.Label(screen,$"Страница {page + 1} из {pages.Length}",new(.43f,.035f),new(.63f,.13f),21);
            if (page > 0) ui.Button(screen,"← Назад",new(.17f,.035f),new(.38f,.13f),() => Read(flow,page - 1));
            if (page + 1 < pages.Length) ui.Button(screen,"Вперёд →",new(.62f,.035f),new(.83f,.13f),() => Read(flow,page + 1));
        }
    }
}
