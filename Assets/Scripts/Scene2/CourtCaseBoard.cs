using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightChase.Station
{
    internal sealed class CourtCaseBoard : MonoBehaviour
    {
        static readonly string[] Titles = { "Групповое фото", "Камера склада № 4", "Вырезка о складе", "Вырезка о суде", "Схема квартала", "Квитанция за фургон" };
        internal static readonly string[] Bodies =
        {
            "Бруклин, 1999. Отец и трое мужчин у синей двери с цифрой 4. У мужчины справа белая нашивка.",
            "AUG 09 1999 · Склад № 4. Мужчина с белой нашивкой у синей двери. Пометка Миллера: «Сверить фигуру справа на групповом фото».",
            "Бруклинская хроника · 11 августа 1999 года\nСклад № 4 закрыт после ночного осмотра\n\nВ среду портовая служба опечатала склад № 4 на территории грузового терминала. Осмотр начался накануне вечером после сообщения дежурного охранника. По его словам, за день до осмотра к складу приходили двое мужчин. Один представился инспектором судоходной компании.\n\nПредставитель порта сообщил, что компания не направляла сотрудников на склад в тот день. Следователи забрали журнал посетителей и запись с камеры над грузовыми воротами.\n\nГрузовые ворота склада № 4, утро среды. См. архивное дело Миллера.",
            "Бруклинская хроника · 12 сентября 1999 года\nПортовое дело рассмотрят в среду\n\nФедеральный суд Ист-Ривер назначил открытое слушание по делу о подложных грузовых документах на среду, 15 сентября. Ожидается, что на заседание придут бывшие сотрудники терминала и семьи портовых рабочих.\n\nРедакция получила копию списка свидетелей. Двое из них работали на том же терминале, где в августе опечатали склад № 4.\n\nФедеральный суд Ист-Ривер. Снимок из архива редакции.",
            "Федеральный суд Ист-Ривер\n\nНа полях: «Суд. 15 сентября. Подтверждено».\nБ. К.",
            "ПРОКАТ ФУРГОНОВ\n\nКвитанция оформлена: 12 сентября 1999 г.\nПлановое получение: 14 сентября 1999 г.\nЗаказчик: Борис Кравец."
        };
        static readonly string[] Slots = { "Кто", "Где и когда", "Что подготовили" };
        static readonly string[] Hints = { "Нужен снимок, где можно узнать отца и людей рядом с ним.",
            "Газета сообщает о заседании. Мне нужно понять, почему оно интересовало отца.",
            "Нужен документ, который показывает, что Кравец подготовил заранее." };
        static readonly int[] Expected = { 0, 4, 5 };
        readonly int[] chosen = { -1, -1, -1 };
        readonly Vector2[] positions = { new(.03f,.68f),new(.35f,.68f),new(.67f,.68f),
            new(.03f,.07f),new(.35f,.07f),new(.67f,.07f) };
        static readonly Vector2 CardSize = new(.29f,.24f);
        readonly RectTransform[] targets = new RectTransform[3];
        readonly TMP_Text[] labels = new TMP_Text[3];
        readonly RectTransform[] threads = new RectTransform[2];
        RectTransform board;
        Action match;
        Action openNorthEntrance;

        internal void Show(Scene2FlowManager flow, Action back, string hint = null, Action onMatch = null, Action onOpenNorthEntrance = null)
        {
            var ui = flow.UI.Workspace;
            if (onMatch != null) match = onMatch;
            if (onOpenNorthEntrance != null) openNorthEntrance = onOpenNorthEntrance;
            var screen = ui.Page("Доска с уликами", back, "Закрыть  [Esc]");
            board = EvidenceWorkspace.Rect(screen,"Доска дела",new(.04f,.15f),new(.96f,.88f));
            board.gameObject.AddComponent<Image>().color = new Color(.36f,.25f,.17f);
            for (int i = 0; i < Slots.Length; i++)
            {
                int slot = i;
                float x = .03f + i * .32f;
                targets[i] = EvidenceWorkspace.Rect(board,Slots[i],new(x,.36f),new(x+.29f,.63f));
                targets[i].gameObject.AddComponent<Image>().color = new Color(.13f,.16f,.17f);
                labels[i] = ui.Label(targets[i],"",new(.06f,.30f),new(.94f,.96f),22);
                UpdateSlot(i);
                ui.Button(targets[i],"Очистить",new(.06f,.04f),new(.94f,.27f),
                    () => { chosen[slot] = -1; UpdateSlot(slot); });
            }
            for (int i = 0; i < Titles.Length; i++)
            {
                int cardIndex = i;
                var card = EvidenceWorkspace.Rect(board,Titles[i],positions[i],positions[i]+CardSize);
                card.gameObject.AddComponent<Image>().color = new Color(.20f,.18f,.16f);
                bool hasArt = AddArt(card,i,new(.015f,.025f),new(.985f,.975f));
                var caption = EvidenceWorkspace.Rect(card,"Подпись",new(.08f,.06f),new(.92f,.30f));
                var captionBackground = caption.gameObject.AddComponent<Image>();
                captionBackground.color = new Color(.92f,.92f,.92f,.94f);
                captionBackground.raycastTarget = false;
                ui.Label(caption,Titles[i],new(.06f,.08f),new(.94f,.92f),20).color = Color.black;
                caption.gameObject.SetActive(!hasArt);
                var drag = card.gameObject.AddComponent<EvidenceCardDrag>();
                if (hasArt) drag.HoverCaption = caption.gameObject;
                drag.Bind(board, position =>
                {
                    positions[cardIndex] = position;
                    DrawThreads();
                }, () => Inspect(flow,back,cardIndex), data =>
                {
                    for (int slot = 0; slot < targets.Length; slot++)
                    {
                        if (!RectTransformUtility.RectangleContainsScreenPoint(targets[slot],data.position,data.pressEventCamera)) continue;
                        chosen[slot] = cardIndex;
                        UpdateSlot(slot);
                        return true;
                    }
                    return false;
                });
            }
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = null;
                if (!InvestigationNotes.For(flow).CourtSolved) continue;
                threads[i] = EvidenceWorkspace.Rect(board,"Нить",Vector2.zero,Vector2.zero);
                var image = threads[i].gameObject.AddComponent<Image>();
                image.color = new Color(.9f,.21f,.15f);
                image.raycastTarget = false;
            }
            Canvas.ForceUpdateCanvases();
            DrawThreads();
            ui.Label(screen,hint ?? "Перетащите карточки в три слота. Нажмите на оригинал для осмотра.",new(.04f,.025f),new(.47f,.135f),22);
            if (openNorthEntrance != null)
                ui.Button(screen,"Северный вход",new(.49f,.04f),new(.72f,.13f),openNorthEntrance);
            ui.Button(screen,"Сопоставить",new(.74f,.04f),new(.96f,.13f),() => Check(flow,back));
        }

        void UpdateSlot(int slot) =>
            labels[slot].text = Slots[slot] + "\n" + (chosen[slot] < 0 ? "Перетащите карточку" : Titles[chosen[slot]]);

        void Inspect(Scene2FlowManager flow, Action back, int index, bool reversed = false)
        {
            var ui = flow.UI.Workspace;
            var screen = ui.Page(Titles[index],() => Show(flow,back),"К доске  [Esc]");
            bool art = !reversed && AddArt(screen,index,new(.06f,.20f),new(.40f,.83f));
            string value = !reversed ? Bodies[index] : index == 0
                ? "Бруклин. 1999. Мы помним."
                : "Пометка Миллера\n\nКравец — крайний справа на групповом снимке.";
            if (reversed)
            {
                var paper = EvidenceWorkspace.Rect(screen,"Оборот",new(.12f,.18f),new(.88f,.86f));
                paper.gameObject.AddComponent<Image>().color = new Color(.82f,.77f,.66f);
                ui.Label(paper,value,new(.08f,.14f),new(.93f,.88f),32).color = new Color(.09f,.18f,.30f);
            }
            else ui.Label(screen,value,new(art ? .43f : .12f,.16f),new(.94f,.86f),29);
            if (index == 0 || index == 5)
                ui.Button(screen,reversed ? "Лицевая сторона" : "Перевернуть",new(.66f,.05f),new(.94f,.13f),
                    () => Inspect(flow,back,index,!reversed));
        }

        internal static bool AddArt(RectTransform parent, int index, Vector2 min, Vector2 max)
        {
            var texture = Resources.Load<Texture2D>("StationEvidence/CourtCase" + index);
            if (!texture) return false;
            var area = EvidenceWorkspace.Rect(parent,"Рамка",min,max);
            var image = EvidenceWorkspace.Rect(area,"Иллюстрация",Vector2.zero,Vector2.one).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            var fit = image.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)texture.width / texture.height;
            return true;
        }

        void Check(Scene2FlowManager flow, Action back)
        {
            for (int i = 0; i < chosen.Length; i++)
            {
                if (chosen[i] == Expected[i]) continue;
                Show(flow,back,chosen[i] < 0 ? "Заполните слот «" + Slots[i] + "»." : Hints[i]);
                return;
            }
            var notes = InvestigationNotes.For(flow);
            notes.CourtSolved = true;
            match?.Invoke();
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Вывод",() => Show(flow,back),"К доске  [Esc]");
            ui.Label(screen,InvestigationNotes.BoardEntry,new(.10f,.22f),new(.90f,.78f),32);
        }

        void DrawThreads()
        {
            if (!board) return;
            for (int i = 0; i < threads.Length; i++)
            {
                var thread = threads[i];
                if (!thread) continue;
                Vector2 a = Vector2.Scale(positions[Expected[i]] + CardSize*.5f,board.rect.size);
                Vector2 b = Vector2.Scale(positions[Expected[i+1]] + CardSize*.5f,board.rect.size);
                Vector2 delta = b-a;
                thread.anchoredPosition = (a+b)*.5f;
                thread.sizeDelta = new Vector2(delta.magnitude,3);
                thread.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            }
        }
    }
}
