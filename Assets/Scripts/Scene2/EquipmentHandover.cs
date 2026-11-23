using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NightChase.Station
{
    public sealed class EquipmentHandover : StationInteractable
    {
        internal const string HeroSurname = "Карамазов";
        static readonly InventoryItem[] Gear = { InventoryItem.Pistol, InventoryItem.IdCard, InventoryItem.Handcuffs };
        static readonly string[] Ids = { "Pistol", "IdCard", "Handcuffs" };
        static readonly string[] Labels = { "Оружие", "Удостоверение", "Наручники" };
        readonly bool[] placed = new bool[3];
        readonly RectTransform[] wells = new RectTransform[3];
        readonly GameObject[] props = new GameObject[3];
        [SerializeField] UnityEvent onComplete = new();
        [SerializeField] Transform clerk, tray;
        Transform eyes, head, idCover;
        Vector3 lookOffset;
        Quaternion eyeRotation, headRest;
        Vector3 eyePosition, idSlot;
        Coroutine cameraMove, reading;
        Scene2FlowManager owner;
        bool active, completed;
        string reaction;
        TextMesh ledger;
        AudioClip[] cues;
        AudioSource audioSource;

        internal void Bind(Transform attendant, Vector3 trayPosition)
        {
            clerk = attendant;
            tray = new GameObject("Лоток приёма вещей").transform;
            tray.SetParent(transform,false);
            tray.position = trayPosition;
            tray.rotation = Quaternion.identity;
            TrayPart("Дно",Vector3.zero,new Vector3(.71f,.025f,.27f),new Color(.14f,.15f,.155f));
            for (int side = -1; side <= 1; side += 2)
            {
                TrayPart("Борт",new Vector3(side*.34f,.025f,0),new Vector3(.027f,.045f,.27f),new Color(.28f,.29f,.30f));
                TrayPart("Борт",new Vector3(0,.025f,side*.125f),new Vector3(.71f,.045f,.027f),new Color(.28f,.29f,.30f));
            }
            var ledgerProp = ItemProps.Create(InventoryItem.Notebook,tray);
            ledgerProp.transform.localPosition = new Vector3(.47f,.017f,0);
            ledger = new GameObject("Запись в журнале").AddComponent<TextMesh>();
            ledger.transform.SetParent(tray,false);
            ledger.transform.localPosition = new Vector3(.47f,.047f,0);
            ledger.transform.localRotation = Quaternion.Euler(90,0,0);
            ledger.anchor = TextAnchor.MiddleCenter;
            ledger.characterSize = .003f;
            ledger.color = Color.white;
            for (int i = 0; i < Gear.Length; i++)
            {
                props[i] = ItemProps.Create(Gear[i],tray);
                props[i].transform.localPosition = new Vector3((i - 1)*.21f,.035f,0);
                props[i].SetActive(false);
            }
            idSlot = props[1].transform.localPosition;
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = "Обложка удостоверения";
            Destroy(cover.GetComponent<Collider>());
            cover.GetComponent<Collider>().enabled = false;
            idCover = cover.transform;
            idCover.SetParent(props[1].transform,false);
            idCover.localPosition = new Vector3(0,.008f,.04f);
            idCover.localScale = new Vector3(.114f,.004f,.073f);
            var coverTint = new MaterialPropertyBlock();
            coverTint.SetColor("_BaseColor",new Color(.19f,.095f,.065f));
            cover.GetComponent<Renderer>().SetPropertyBlock(coverTint);
            var animator = clerk.GetComponentInChildren<Animator>();
            if (animator && animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        void TrayPart(string name,Vector3 position,Vector3 size,Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(tray,false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<Collider>().enabled = false;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor",color);
            part.GetComponent<Renderer>().SetPropertyBlock(tint);
        }

        public override string Prompt(Scene2FlowManager flow) =>
            completed ? "E — Поговорить с дежурной" :
            flow.State == Scene2FlowManager.Stage.HandoverEquipment ? "E — Сдать вещи" : "";

        public override void Use(Scene2FlowManager flow)
        {
            if (completed) { flow.Notify("Дежурная: вещи уже приняты.",3); return; }
            if (active || string.IsNullOrEmpty(Prompt(flow))) return;
            owner = flow;
            active = true;
            reaction = "Выложите вещи в лоток.";
            flow.Lock(true);
            var camera = flow.Player.GetComponentInChildren<Camera>();
            if (camera && tray)
            {
                eyes = camera.transform;
                eyePosition = eyes.localPosition;
                eyeRotation = eyes.localRotation;
                cameraMove = StartCoroutine(Approach());
            }
            if (head) headRest = head.localRotation;
            Show();
        }

        IEnumerator Approach()
        {
            Vector3 start = eyes.position;
            Quaternion rotation = eyes.rotation;
            Vector3 focus = tray.position + Vector3.up * .25f;
            Vector3 toward = focus - start;
            Vector3 goal = start + toward.normalized * Mathf.Clamp(toward.magnitude - .8f,0,.65f);
            if (Physics.Linecast(start,goal,out var obstruction,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                goal = obstruction.point - toward.normalized * .08f;
            var facing = toward.sqrMagnitude > .001f ? Quaternion.LookRotation(toward) : rotation;
            for (float t = 0; t < .36f; t += Time.unscaledDeltaTime)
            {
                float blend = Mathf.SmoothStep(0,1,t/.36f);
                eyes.SetPositionAndRotation(Vector3.Lerp(start,goal,blend),Quaternion.Slerp(rotation,facing,blend));
                yield return null;
            }
            eyes.SetPositionAndRotation(goal,facing);
            cameraMove = null;
        }

        void Show()
        {
            var ui = owner.UI.Workspace;
            var screen = ui.Page("Сдача вещей",owner.ClosePanel,"Выйти  [Esc]");
            screen.GetComponent<Image>().color = new Color(.05f,.065f,.07f,.46f);
            ui.Label(screen,"Дежурная: " + reaction,new(.055f,.73f),new(.95f,.88f),25);
            var surface = EvidenceWorkspace.Rect(screen,"Инвентарь и лоток",new(.055f,.15f),new(.945f,.70f));
            ui.Label(surface,"Лоток",new(.03f,.83f),new(.96f,.99f),26);
            ui.Label(surface,"Ваш инвентарь",new(.03f,.26f),new(.96f,.41f),24);
            for (int i = 0; i < Ids.Length; i++)
            {
                int index = i;
                float x = .025f + i*.323f;
                var start = new Vector2(x,.45f);
                var end = new Vector2(x+.30f,.82f);
                var slot = wells[i] = EvidenceWorkspace.Rect(surface,Labels[i],start,end);
                slot.gameObject.AddComponent<Image>().color = new Color(.14f,.17f,.18f,.91f);
                ui.Label(slot,Labels[i],new(.05f,.68f),new(.96f,.98f),22);
                if (placed[i])
                    ui.Button(slot,"Вернуть",new(.05f,.08f),new(.96f,.41f),() => SetPlaced(index,false));
                if (!owner.Inventory.Has(Ids[i])) continue;
                if (placed[i]) AddIcon(slot,i,new(.34f,.36f),new(.66f,.70f));
                else
                {
                    var card = EvidenceWorkspace.Rect(surface,Labels[i],new(x,.015f),new(x+.30f,.24f));
                    card.gameObject.AddComponent<Image>().color = new Color(.24f,.29f,.30f,.96f);
                    AddIcon(card,i,new(.05f,.12f),new(.31f,.87f));
                    ui.Label(card,i == 0 ? "Пистолет" : Labels[i],new(.34f,.08f),new(.96f,.92f),22);
                    card.gameObject.AddComponent<EvidenceCardDrag>().Bind(surface,_ => {},() => SetPlaced(index,true), data =>
                    {
                        if (!RectTransformUtility.RectangleContainsScreenPoint(wells[index],data.position,data.pressEventCamera)) return true;
                        SetPlaced(index,true);
                        return true;
                    });
                }
            }
            if (Ready && reading == null)
                ui.Button(screen,"Подтвердить сдачу",new(.69f,.035f),new(.945f,.125f),Confirm);
        }

        static void AddIcon(RectTransform parent,int index,Vector2 min,Vector2 max)
        {
            var texture = Resources.Load<Texture2D>("InventoryIcons/" + Ids[index]);
            if (!texture) return;
            var icon = EvidenceWorkspace.Rect(parent,"Предмет",min,max).gameObject.AddComponent<RawImage>();
            icon.texture = texture;
            icon.raycastTarget = false;
            var fit = icon.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)texture.width / texture.height;
        }

        bool Ready => placed[0] && placed[1] && placed[2];

        void SetPlaced(int index,bool value)
        {
            if (!active || placed[index] == value || !owner.Inventory.Has(Ids[index])) return;
            placed[index] = value;
            if (props[index]) props[index].SetActive(value);
            if (!value && index == 1) EndReading();
            if (!value) reaction = "Предмет возвращён.";
            else
            {
                PlayCue(index);
                reaction = index == 0 ? "Оружие вижу." : index == 2 ? "Наручники на месте." : "Удостоверение…";
                if (index == 1) reading = StartCoroutine(ReadId());
            }
            if (Ready && reading == null) reaction = "Всё на месте. Оформляем?";
            Show();
        }

        IEnumerator ReadId()
        {
            var card = props[1] ? props[1].transform : null;
            Vector3 pull = clerk && tray ? tray.InverseTransformDirection((clerk.position - tray.position).normalized) : Vector3.forward;
            pull.y = 0;
            for (float t = 0; t < .4f; t += Time.unscaledDeltaTime)
            {
                float blend = Mathf.SmoothStep(0,1,t/.4f);
                if (card) card.localPosition = idSlot + pull*.09f*blend;
                lookOffset = new Vector3(22f*blend,0,0);
                if (idCover) idCover.localRotation = Quaternion.Euler(-105f*blend,0,0);
                yield return null;
            }
            reaction = HeroSurname + "…";
            Show();
            yield return new WaitForSecondsRealtime(.75f);
            if (head && eyes)
            {
                var parent = head.parent;
                Vector3 toward = parent.InverseTransformDirection(eyes.position-head.position);
                float sideways = Mathf.Clamp(toward.x * 35,-22,22);
                lookOffset = new Vector3(-6,sideways,0);
            }
            yield return new WaitForSecondsRealtime(.65f);
            reading = null;
            lookOffset = Vector3.zero;
            if (idCover) idCover.localRotation = Quaternion.identity;
            if (head) head.localRotation = headRest;
            if (card) card.localPosition = idSlot;
            reaction = Ready ? "Всё на месте. Оформляем?" : "Удостоверение принято к сверке.";
            Show();
        }

        void EndReading()
        {
            if (reading != null) StopCoroutine(reading);
            reading = null;
            lookOffset = Vector3.zero;
            if (idCover) idCover.localRotation = Quaternion.identity;
            if (head) head.localRotation = headRest;
            if (props[1]) props[1].transform.localPosition = idSlot;
        }

        void LateUpdate()
        {
            if (active && head && reading != null)
                head.localRotation = headRest * Quaternion.Euler(lookOffset);
        }

        void Confirm()
        {
            if (!active || completed || !Ready || reading != null) return;
            var flow = owner;
            if (!flow.Inventory.RemoveAll(Ids))
            {
                reaction = "Комплект изменился. Проверьте инвентарь.";
                Show();
                return;
            }
            completed = true;
            if (ledger) ledger.text = HeroSurname + "\nКомплект принят";
            flow.ClosePanel();
            flow.BeginInvestigationAfterHandover();
            flow.Notify("Дежурная: принято. Можете проходить.",4);
            onComplete.Invoke();
        }

        internal void Close()
        {
            if (!active) return;
            active = false;
            EndReading();
            if (cameraMove != null) StopCoroutine(cameraMove);
            cameraMove = null;
            if (eyes) eyes.SetLocalPositionAndRotation(eyePosition,eyeRotation);
            eyes = null;
            if (!completed)
                for (int i = 0; i < placed.Length; i++)
                {
                    placed[i] = false;
                    if (props[i]) props[i].SetActive(false);
                }
            owner = null;
        }

        void PlayCue(int index)
        {
            if (!audioSource)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                cues = new AudioClip[3];
            }
            if (!cues[index])
            {
                var samples = new float[2800];
                for (int i = 0; i < samples.Length; i++)
                {
                    float time = i / 22050f;
                    float envelope = Mathf.Exp(-time * (index == 2 ? 38f : 57f));
                    float wave = index == 0 ? Mathf.Sin(time * 380f) : index == 2 ?
                        (Mathf.Sin(time*7700f) + Mathf.Sin(time*10500f))*.35f : UnityEngine.Random.Range(-.4f,.4f);
                    samples[i] = wave * envelope * .42f;
                }
                cues[index] = AudioClip.Create(Labels[index],samples.Length,1,22050,false);
                cues[index].SetData(samples,0);
            }
            audioSource.PlayOneShot(cues[index],.55f);
        }

        void OnDestroy()
        {
            if (cues != null)
                foreach (var clip in cues) if (clip) Destroy(clip);
        }
    }
}
