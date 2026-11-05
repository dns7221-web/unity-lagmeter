#if UNITY_EDITOR || DEVELOPMENT_BUILD   // LagMeter 자체가 이 조건에서만 컴파일된다
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// 추가 생성 (2026-11-05) — 렉 미터 플레이 모드 테스트.
//
// 렉 미터는 MonoBehaviour 하나라 계산 로직이 private에 들어 있다. 세 프로젝트에서 잘 쓰는 코드를 테스트하려고
// 구조를 뜯어고치는 대신, <b>밖에서 보이는 약속</b>만 확인한다:
//   1. Play를 누르면 씬에 넣지 않아도 저절로 생기고, 씬이 바뀌어도 남는다(DontDestroyOnLoad).
//   2. 키보드가 없는 환경(배치 모드·CI)에서도 오류 없이 프레임을 잰다.
//   3. "Play 시작부터 녹화"를 켜면 끝날 때 CSV·요약이 저장되고, 프로젝트가 등록한 카운터가 CSV에 한 번만 실린다.
//
// 순서가 있다: 3번 테스트는 자동으로 생긴 렉 미터를 치우고 새로 만들어 쓰므로 맨 마지막에 돈다.

namespace DHS.Diagnostics.Tests
{
    public class LagMeterPlayModeTests
    {
        [UnityTest, Order(1)]
        public IEnumerator AutoCreatesOnPlay_AndSurvivesSceneLoads()
        {
            yield return null;

            LagMeter meter = Object.FindAnyObjectByType<LagMeter>();
            Assert.That(meter, Is.Not.Null, "Play 시작 때 저절로 생기지 않음([RuntimeInitializeOnLoadMethod])");
            Assert.That(meter.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
        }

        [UnityTest, Order(2)]
        public IEnumerator Measures120FramesWithoutErrors()
        {
            // 배치 모드에는 키보드가 없다(Keyboard.current == null). 그래도 예외 없이 돌아야 한다.
            // 테스트 러너는 Error·Exception 로그가 나오면 테스트를 실패로 만든다.
            for (int i = 0; i < 120; i++)
                yield return null;

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Order(3)]
        public IEnumerator RecordFromStart_SavesCsvWithRegisteredCounterOnce()
        {
            // 자동으로 붙은 것은 치우고, "Play 시작부터 녹화"를 켠 상태로 새로 만든다(설정은 Awake에서 읽는다).
            foreach (LagMeter existing in Object.FindObjectsByType<LagMeter>(FindObjectsSortMode.None))
                Object.Destroy(existing.gameObject);
            yield return null;

            int previous = PlayerPrefs.GetInt(LagMeter.RecordFromStartPrefKey, 0);
            PlayerPrefs.SetInt(LagMeter.RecordFromStartPrefKey, 1);

            // 같은 키를 두 번 등록한다 — 도메인 리로드 설정에 따라 실제로 일어나는 일이다. CSV에는 한 열만 있어야 한다.
            float counter = 0f;
            LagMeter.RegisterCounter("TestCounter", "테스트 값", () => counter);
            LagMeter.RegisterCounter("TestCounter", "중복 등록", () => -1f);

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProfilerCaptures");
            HashSet<string> before = Directory.Exists(folder)
                ? new HashSet<string>(Directory.GetFiles(folder))
                : new HashSet<string>();

            GameObject go = new GameObject("LagMeterUnderTest");
            go.AddComponent<LagMeter>();
            for (int i = 1; i <= 30; i++)
            {
                counter = i;
                yield return null;
            }

            Object.Destroy(go);   // OnDestroy가 녹화 중이면 저장한다
            yield return null;
            PlayerPrefs.SetInt(LagMeter.RecordFromStartPrefKey, previous);

            List<string> created = Directory.Exists(folder)
                ? Directory.GetFiles(folder).Where(f => !before.Contains(f)).ToList()
                : new List<string>();
            try
            {
                string csv = created.SingleOrDefault(f => f.EndsWith(".csv", StringComparison.Ordinal));
                Assert.That(csv, Is.Not.Null, "CSV가 저장되지 않음");
                Assert.That(created.Any(f => f.EndsWith("_summary.txt", StringComparison.Ordinal)), Is.True, "요약이 저장되지 않음");

                string[] lines = File.ReadAllLines(csv);
                string[] header = lines[0].Split(',');
                Assert.That(header.Count(h => h == "TestCounter"), Is.EqualTo(1), "등록한 카운터 열이 한 번만 있어야 함");
                Assert.That(lines.Length - 1, Is.GreaterThanOrEqualTo(25), "프레임 기록이 너무 적음");

                // 카운터 값이 실제로 실렸는지 — 마지막 줄은 0보다 커야 한다(1부터 올렸으므로).
                int column = Array.IndexOf(header, "TestCounter");
                float last = float.Parse(lines[lines.Length - 1].Split(',')[column], CultureInfo.InvariantCulture);
                Assert.That(last, Is.GreaterThan(0f));
            }
            finally
            {
                // 테스트가 만든 기록 파일은 지운다(프로젝트의 ProfilerCaptures를 어지르지 않게).
                foreach (string file in created)
                    File.Delete(file);
            }
        }
    }
}
#endif
