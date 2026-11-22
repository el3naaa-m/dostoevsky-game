using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace NightChase
{
    sealed class ChaseFight : MonoBehaviour
    {
        const float RoundTime = 1.65f;
        const float WindowStart = .82f;
        const float WindowEnd = 1.18f;
        const int HitGoal = 3;
        const int MissLimit = 3;

        Transform player, eyes;
        ChaseRunner suspect;
        float elapsed, feedbackUntil;
        int hits, misses;
        bool inRange, resolved, won;
        string feedback;
        GUIStyle textStyle;

        public void Begin(Transform playerRoot, Transform playerEyes, ChaseRunner opponent)
        {
            player = playerRoot;
            eyes = playerEyes;
            suspect = opponent;
            suspect.Face(player.position);
        }

        void Update()
        {
            if (!suspect || resolved) return;

            Vector3 distance = player.position - suspect.transform.position;
            distance.y = 0;
            inRange = distance.sqrMagnitude <= 3.2f * 3.2f;
            if (!inRange)
            {
                suspect.Reach(null);
                return;
            }

            suspect.Face(player.position, 540f * Time.deltaTime);
            if (Time.time < feedbackUntil) return;

            elapsed += Time.deltaTime;
            bool windowOpen = elapsed >= WindowStart && elapsed <= WindowEnd;
            suspect.Reach(windowOpen ? eyes : null);

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                ResolveRound(windowOpen);
            else if (elapsed >= RoundTime)
                ResolveRound(false);
        }

        void ResolveRound(bool hit)
        {
            if (hit) hits++;
            else misses++;

            elapsed = 0;
            feedback = hit ? "Попадание" : "Промах";
            feedbackUntil = Time.time + .55f;
            suspect.Reach(null);
            won = hits >= HitGoal;
            resolved = won || misses >= MissLimit;
            if (!won && resolved) Invoke(nameof(RestartStreet), .9f);
        }

        void RestartStreet()
        {
            string path = SceneManager.GetActiveScene().path;
#if UNITY_EDITOR
            if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,
                    new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(path);
        }

        void OnGUI()
        {
            if (!suspect) return;
            textStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            textStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * .028f), 16, 40);
            textStyle.normal.textColor = Color.white;

            float width = Mathf.Min(Screen.width * .84f, 570f);
            var panel = new Rect((Screen.width - width) / 2, Screen.height * .72f, width, Screen.height * .19f);
            GUI.Box(panel, GUIContent.none);

            string prompt = "Драка · дождись зелёной зоны и нажми E";
            if (won) prompt = "Преступник обезврежен";
            else if (resolved) prompt = "Три промаха · перезапуск";
            else if (!inRange) prompt = "Подойди к преступнику";
            else if (Time.time < feedbackUntil) prompt = feedback;

            float lineHeight = panel.height / 3f;
            GUI.Label(new Rect(panel.x, panel.y, width, lineHeight), prompt, textStyle);
            GUI.Label(new Rect(panel.x, panel.y + lineHeight, width, lineHeight),
                $"Попадания {hits}/{HitGoal}     Промахи {misses}/{MissLimit}", textStyle);
            if (resolved || !inRange || Time.time < feedbackUntil) return;

            var track = new Rect(panel.x + 24, panel.y + lineHeight * 2.25f, width - 48, lineHeight * .32f);
            Fill(track, new Color(.22f, .22f, .22f));
            Fill(new Rect(track.x + track.width * WindowStart / RoundTime, track.y,
                track.width * (WindowEnd - WindowStart) / RoundTime, track.height), new Color(.18f, .68f, .28f));
            Fill(new Rect(track.x + track.width * Mathf.Clamp01(elapsed / RoundTime) - 2,
                track.y - 3, 4, track.height + 6), Color.white);
        }

        static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
