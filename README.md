# 렉 미터 (LagMeter)

> Profiler 창을 열지 않고, **Play 중 게임 화면에서** "렉이 났는지, 그때 뭐가 무거웠는지"를 본다.

유니티 내장 `Unity.Profiling.ProfilerRecorder`로 Profiler 창이 보여주는 값(구간 시간 ·
GC 할당 · 배치 수)을 **코드에서 직접** 읽는다. 그래서 창을 열지 않아도 되고,
**빌드한 실행 파일 안에서도** 숫자가 뜬다.

| 키 | |
|---|---|
| **F8** | 표시 켜기 / 끄기 (끈 상태는 다음 Play에도 기억) |
| **F9** | 녹화 시작 / 저장 → `ProfilerCaptures/`에 프레임별 CSV + 한국어 요약 |
| **F10** | 지금 화면을 PNG 한 장으로 → 같은 폴더에 `shot_<날짜시각>.png` |

`Tools ▸ 렉 미터 ▸ Play 시작부터 녹화`를 켜 두면 F9를 누를 틈도 없이 지나가는
**시작 직후 렉**도 잡힌다.

---

## 넣는 법

Package Manager ▸ **＋** ▸ *Add package from git URL…*

```
https://github.com/<계정>/unity-lagmeter.git
```

끝이다. **씬에 아무것도 넣지 않는다** — `[RuntimeInitializeOnLoadMethod]`로 Play 때
스스로 생기고 `DontDestroyOnLoad`로 씬이 바뀌어도 계속 잰다.

`.gitignore`에 녹화 폴더를 빼 두는 것만 하면 좋다:

```gitignore
/[Pp]rofiler[Cc]aptures/
```

### 요구 사항

- Unity 6000.0 이상 (`ProfilerRecorder`는 2020.2+면 되지만, 내장 마커 이름은
  이 버전대에서 확인했다)
- Input System (`com.unity.inputsystem`) — 패키지 의존성에 적혀 있어 자동으로 따라온다

---

## 프로젝트 코드 구간을 재려면

렉 미터는 **자기가 뭘 재는지 모른다.** 유니티 내장 항목(물리 · 스크립트 · 렌더링 ·
GC · 배치)만 알고, 프로젝트 코드 구간은 **프로젝트가 등록한다.**

> **이게 이 패키지의 설계 핵심이다.** 재고 싶은 구간 이름을 패키지 안에 적으면 그
> 파일이 특정 프로젝트 전용이 되어, 다른 작업물에 넣을 때마다 패키지를 고쳐야 한다.
> **고칠 데가 있는 파일은 공유할 수 없다.** 그래서 방향을 뒤집었다.

### ① 재고 싶은 구간에 마커를 감는다

```csharp
using Unity.Profiling;

public class GridManager : MonoBehaviour
{
    private static readonly ProfilerMarker s_recycleMarker = new ProfilerMarker("GridManager.Recycle");

    private void RecycleTrailingLine(Vector2Int dir)
    {
        using var _ = s_recycleMarker.Auto();
        // ... 재고 싶은 일 ...
    }
}
```

프로파일러가 꺼진 빌드에서는 유니티가 이 구간을 사실상 비용 없이 넘긴다.

### ② 그 이름을 등록한다

프로젝트에 이런 파일 하나를 둔다 (이름은 아무래도 좋다).

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using DHS.Diagnostics;
using UnityEngine;

public static class LagMeterRows
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        // (마커 이름, 표에 보일 이름, 들여 쓸 부모 줄)
        LagMeter.RegisterMarker("GridManager.Recycle", "격자 재활용", "FixedUpdate.PhysicsFixedUpdate");
        LagMeter.RegisterMarker("CoverageAudit.Check", "└ 은폐 검사", "GridManager.Recycle");
        LagMeter.RegisterMarker("Stalker.Sight", "추격자 시야", "Update.ScriptRunBehaviourUpdate");
    }
}
#endif
```

**순서는 유니티가 보장한다** — 등록은 `BeforeSceneLoad`, 렉 미터 생성은 `AfterSceneLoad`.

부모 줄로 쓸 만한 내장 단계 이름:

| 이름 | |
|---|---|
| `Update.ScriptRunBehaviourUpdate` | `Update()` |
| `FixedUpdate.ScriptRunBehaviourFixedUpdate` | `FixedUpdate()` |
| `PreLateUpdate.ScriptRunBehaviourLateUpdate` | `LateUpdate()` |
| `FixedUpdate.PhysicsFixedUpdate` | 물리 — **트리거·충돌 콜백이 여기서 불린다** |

> 마커는 **처음 실제로 돌 때** 프로파일러에 등록된다. 그래서 아직 한 번도 안 지나간
> 구간은 잠시 "측정 안 됨"으로 보인다 — 렉 미터가 1초마다 다시 찾으니 그 구간이
> 한 번 돌면 숫자가 생긴다. 버그가 아니다.

---

## 프로젝트가 들고 있는 숫자를 재려면 (1.1.0)

`ProfilerMarker`로는 **"이 구간이 몇 ms냐"**만 잴 수 있다. 그런데 프로젝트만 아는
**숫자**를 녹화에 남기고 싶은 경우가 있다 — 위반 횟수, 좌표, 진행도 같은 것.

```csharp
LagMeter.RegisterCounter("CoverageViolations", "은폐 위반", () => audit.ViolationCount);
```

| 인자 | |
|---|---|
| `key` | CSV 머리글에 쓸 이름 |
| `label` | 화면 아래 줄에 보일 이름 |
| `read` | 값을 읽는 함수 — **매 프레임 불린다** |
| `format` | 표시 형식 (기본 `"0"`) |

등록 시점은 `RegisterMarker`와 같다(`BeforeSceneLoad`). 표 아래 줄에 보이고,
**녹화 CSV에 열이 하나 생긴다.**

> ⚠️ `read`는 매 프레임 불리니 **필드 하나 읽기 수준으로 싸야 한다.** 대상이 아직 씬에
> 없을 수 있으므로(`null`) 람다 안에서 확인하고 0을 돌려줄 것. `FindAnyObjectByType`를
> 쓸 거면 **찾은 뒤 캐시**하고, 못 찾았을 때 매 프레임 다시 찾지 않게 간격을 둘 것.
>
> 예외를 던지면 그 줄만 끄고 경고를 한 번 남긴다 — 측정 도구가 게임을 멈추게 하면 안 된다.

---

## 녹화 결과

F9로 저장하면 `ProfilerCaptures/`에 둘이 떨어진다.

| 파일 | |
|---|---|
| `lag_<날짜시각>.csv` | 프레임별 전부. 스프레드시트로 열어 그래프를 그리거나 조건별로 비교 |
| `lag_<날짜시각>_summary.txt` | 한국어 요약 — 튄 프레임이 몇 번, 그때 뭐가 제일 컸는지 |
| `shot_<날짜시각>.png` | **F10**으로 남긴 화면 한 장 (1.2.0) |

요약 파일을 그대로 Claude에게 읽히면 숫자로 원인을 짚을 수 있다.

### 화면 한 장 (F10) — 왜 영상이 아닌가

영상 녹화(Unity Recorder)는 창을 열고 버튼을 눌러야 하고, 프레임레이트를 고정해
**성능 수치를 왜곡한다**(그 설정이 게임 시간을 붙잡는다). 반면 *"어 이상한데"* 싶은 순간은
대개 **한 장으로 충분하다** — 문이 안 닫혔다, 칸이 튀었다, 소품이 벽을 뚫었다.
키 하나로 증거가 남는 쪽이 실제로 더 쓰인다.

**표시판이 켜져 있으면 같이 찍힌다**(게임 뷰를 그대로 뜨므로). 그 순간의 FPS·튄 프레임·표가
그림 안에 함께 남아서, 나중에 "이때 수치가 뭐였나"를 따로 찾을 필요가 없다.

> ⚠️ `ScreenCapture.CaptureScreenshot`은 **비동기**다 — 프레임이 끝난 뒤에 파일을 쓴다.
> 누른 직후에 폴더를 열면 아직 없을 수 있다.

---

## 구조

```
Runtime/
  LagMeter.cs                   측정 · 화면 표시(IMGUI) · 녹화. 프로젝트를 모른다
  DHS.Diagnostics.asmdef        → Unity.InputSystem 참조
Editor/
  LagMeterMenu.cs               Tools ▸ 렉 미터 메뉴
  DHS.Diagnostics.Editor.asmdef → includePlatforms: ["Editor"]
```

**에디터 코드를 따로 뺀 이유** — 런타임 어셈블리에 `UnityEditor` 참조를 남기지 않기
위해서다. 한 파일에 `#if UNITY_EDITOR`로 섞어도 돌아가지만, asmdef를 쓰는 순간 그
경계는 공짜로 얻을 수 있다.

> ⚠️ **폴더에도 `.meta`가 있어야 한다** (1.2.1에서 고침). git 패키지는 읽기 전용이라
> 유니티가 `.meta`를 스스로 만들 수 없고, 없으면 임포트마다 이렇게 경고한다:
>
> ```
> Asset Packages/com.dhs.lagmeter/Runtime has no meta file,
> but it's in an immutable folder. The asset will be ignored.
> ```
>
> 스크립트 컴파일은 그래도 되지만(어셈블리는 정상적으로 생긴다) **경고가 매번 반복돼
> 콘솔에서 진짜 에러를 가린다.** 유니티 자기 패키지들도 `Runtime.meta`를 함께 넣는다 —
> 파일만 챙기고 폴더를 빠뜨리기 쉬운 자리다.

파일 전체가 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`라 **최종 빌드엔 아예 들어가지 않는다.**

## 도구가 렉을 만들면 안 된다

- 글자는 정해진 간격(기본 0.25초)마다만 새로 만든다 — 매 프레임 만들면 이 도구가
  쓰레기(GC)를 내서 스스로 렉을 낸다
- 그래프는 텍스처 한 장에 프레임마다 한 줄씩만 칠한다
- 표 정렬은 미리 만들어 둔 비교자를 쓴다 (람다로 정렬하면 정렬할 때마다 할당이 생긴다)
- 표에 **"렉 미터 — Update"** / **"렉 미터 — OnGUI"** 두 줄이 있다 — 도구가 무거워지면
  바로 보이고, **어느 쪽이 무거운지까지** 보인다
- 그래프 텍스처는 1초에 30번만 GPU로 올린다. 프레임마다 한 칸씩만 고치는데 `Apply()`는
  텍스처 전체(300×64 ≈ 77KB)를 올리므로, 155fps에서 매 프레임 올리면 12MB/초가 된다

> **1.0.0에서 실제로 걸렸던 것 —** 자기 비용이 매 프레임 0.66ms(최고 26.28ms)였다.
> 측정 대상이 78초에 5번·평균 1.6ms인데 도구가 평균 프레임의 10%를 먹어, **재는 값
> 자체를 왜곡**하고 있었다. 1.1.0에서 Update/OnGUI를 갈라 범인을 좁힐 수 있게 하고
> 그래프 업로드를 제한했다.

## 어디서 왔나

웹스윙 게임(Momentum) 작업 중에 만들었고, 영안실 호러 게임(Morgue Room Horror)에
가져다 쓰면서 **프로젝트 전용 코드를 걷어내고** 패키지로 떼어냈다.
