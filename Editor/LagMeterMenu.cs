using UnityEditor;
using UnityEngine;

namespace DHS.Diagnostics.Editor
{
    /// <summary>
    /// 렉 미터의 에디터 메뉴 — <c>Tools ▸ 렉 미터</c>.
    ///
    /// ── 왜 런타임 파일에서 갈라냈나 ────────────────────────
    /// 원래는 <see cref="LagMeter"/> 파일 끝에 <c>#if UNITY_EDITOR</c>로 같이 들어 있었다.
    /// 그래도 동작은 하지만, 패키지로 떼어내면서 <b>런타임 어셈블리에 UnityEditor 참조가
    /// 남지 않게</b> 분리했다 — 런타임 어셈블리가 에디터 전용 타입을 알고 있으면 플랫폼별
    /// 빌드에서 사고가 나기 쉽고, asmdef를 쓰는 순간 그 경계는 공짜로 얻을 수 있다.
    ///
    /// 이 어셈블리(<c>DHS.Diagnostics.Editor</c>)는 <c>includePlatforms: ["Editor"]</c>라
    /// 애초에 빌드에 들어가지 않는다.
    ///
    /// ── 왜 인스펙터가 아니라 메뉴인가 ──────────────────────
    /// 렉 미터는 Play를 누르면 <b>저절로 생기는</b> 오브젝트라(씬에 없다) 인스펙터에서 미리
    /// 체크해 둘 자리가 없다. 그래서 설정을 PlayerPrefs에 두고 메뉴로 켜고 끈다.
    /// </summary>
    internal static class LagMeterMenu
    {
        private const string RecordFromStartMenu = "Tools/렉 미터/Play 시작부터 녹화";

        /// <summary>
        /// "Play 시작부터 녹화"를 켜고 끈다. 값은 PlayerPrefs에 둬서 Play 때 LagMeter의 Awake가 바로 읽는다.
        ///
        /// 켜 두면 F9를 누를 틈도 없이 지나가는 <b>시작 직후 렉</b>을 잡을 수 있다.
        /// 녹화는 F9로 저장하고, 안 누르고 Play를 끝내도 LagMeter가 저장한다.
        /// </summary>
        [MenuItem(RecordFromStartMenu)]
        private static void ToggleRecordFromStart()
        {
            bool on = PlayerPrefs.GetInt(LagMeter.RecordFromStartPrefKey, 0) == 0;
            PlayerPrefs.SetInt(LagMeter.RecordFromStartPrefKey, on ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log(on
                ? "[LagMeter] 다음 Play부터 첫 프레임부터 녹화한다 — F9나 Play 끝내기로 저장."
                : "[LagMeter] Play 시작부터 녹화를 껐다 — 다시 F9로 직접 녹화한다.");
        }

        /// <summary>메뉴 옆 체크 표시를 지금 값에 맞춘다.</summary>
        [MenuItem(RecordFromStartMenu, true)]
        private static bool ToggleRecordFromStartValidate()
        {
            Menu.SetChecked(RecordFromStartMenu, PlayerPrefs.GetInt(LagMeter.RecordFromStartPrefKey, 0) == 1);
            return true;
        }
    }
}
