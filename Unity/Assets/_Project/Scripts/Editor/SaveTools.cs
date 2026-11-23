using System.Collections.Generic;
using GameOff.Game;
using UnityEditor;
using UnityEngine;

namespace GameOff.EditorTools
{
    /// <summary>
    /// 저장 데이터 도구(W-034, D-124). 이 PC(Editor)의 진행 기록·설정·언어를 지워 「저장 데이터가 없는 첫 실행」 상태로 되돌린다.
    /// 저장은 PlayerPrefs만 쓴다(Assets/ 안에 저장 파일이 생기지 않고, 빌드에 저장 값이 들어가지 않는다).
    /// 회사·제품 이름이 기본값(DefaultCompany/Unity)이라 PlayerPrefs 저장 위치를 다른 기본 이름 프로젝트와 나눠 쓰므로, DeleteAll 대신 이 게임의 키만 지운다.
    /// </summary>
    public static class SaveTools
    {
        [MenuItem("GameOff/Clear Save")]
        public static void ClearSave()
        {
            var list = AssetDatabase.LoadAssetAtPath<GameOff.Data.RequestList>("Assets/_Project/Data/RequestList.asset");
            var ids = new List<string>();
            if (list != null) foreach (var e in list.entries) ids.Add(e.id);

            bool testFlag = EditorPrefs.GetBool(ProgressStore.TestStoreFlag, false);
            try
            {
                EditorPrefs.SetBool(ProgressStore.TestStoreFlag, false);
                new ProgressStore().ClearAll(ids);   // 실제 저장 공간
                EditorPrefs.SetBool(ProgressStore.TestStoreFlag, true);
                new ProgressStore().ClearAll(ids);   // 자동 확인 전용 저장 공간
            }
            finally
            {
                EditorPrefs.SetBool(ProgressStore.TestStoreFlag, testFlag);
            }
            Debug.Log("[SaveTools] 이 게임의 저장 데이터(진행 기록·설정·언어)를 지웠습니다. 다음 실행은 첫 실행(영어, 진행 0)입니다."
                + (Application.isPlaying ? " 지금 Play 중이라 화면의 값은 다시 실행해야 바뀝니다." : ""));
        }
    }
}
