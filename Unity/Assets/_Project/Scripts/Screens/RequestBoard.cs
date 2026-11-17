using System.Collections.Generic;
using GameOff.Data;

namespace GameOff.Game
{
    public enum RequestState { Locked, New, Clear }

    /// <summary>의뢰 목록 + 진행 저장을 묶어 챕터·의뢰 잠금과 수락 가능 여부를 계산한다(챕터 폴더는 W-005, 02-03 §5-3).</summary>
    public sealed class RequestBoard
    {
        /// <summary>바탕화면에 보이는 챕터 폴더 수(챕터 0~2).</summary>
        public const int ChapterCount = 3;

        readonly RequestList list;
        readonly ProgressStore progress;

        public RequestBoard(RequestList list, ProgressStore progress)
        {
            this.list = list;
            this.progress = progress;
        }

        public int Count => list.entries.Length;
        public RequestEntry Entry(int i) => list.entries[i];
        public int Best(int i) => progress.GetBest(list.entries[i].id);

        /// <summary>튜토리얼 의뢰(별 없음, D-074).</summary>
        public bool IsTutorial(int i) => list.entries[i].level != null && list.entries[i].level.tutorial;

        /// <summary>클리어한 의뢰인가(W-008). 클리어 기록이 있거나, 이전 저장의 별이 1개 이상이면 클리어.</summary>
        public bool IsCleared(int i) => progress.IsCleared(list.entries[i].id) || Best(i) >= 1;

        // ---------- 챕터 ----------

        /// <summary>폴더 아이콘 이름: CH0_HOME(W-007), 아직 없는 챕터는 CH1_???.</summary>
        public static string ChapterName(int chapter) => chapter == 0 ? "CH0_HOME" : "CH" + chapter + "_???";

        public int ChapterOf(int i) => list.entries[i].chapter;

        /// <summary>그 챕터에 속한 의뢰의 목록 번호(목록 순서대로).</summary>
        public List<int> InChapter(int chapter)
        {
            var r = new List<int>();
            for (int i = 0; i < list.entries.Length; i++)
                if (list.entries[i].chapter == chapter) r.Add(i);
            return r;
        }

        /// <summary>챕터 안 몇 번째 의뢰인가(0부터).</summary>
        public int IndexInChapter(int i) => InChapter(ChapterOf(i)).IndexOf(i);

        /// <summary>"0-1" 같은 의뢰 번호(챕터-순서, 1부터).</summary>
        public string Number(int i) => ChapterOf(i) + "-" + (IndexInChapter(i) + 1);

        /// <summary>바탕화면 아이콘 이름: REQ_0-1.</summary>
        public string RequestName(int i) => "REQ_" + Number(i);

        /// <summary>
        /// 챕터가 열려 있는가: 플레이할 수 있는 의뢰(레벨)가 있고, 앞 챕터의 의뢰가 모두 클리어.
        /// 의뢰가 없는 챕터는 항상 잠김.
        /// </summary>
        public bool ChapterOpen(int chapter)
        {
            bool any = false;
            for (int i = 0; i < list.entries.Length; i++)
            {
                RequestEntry e = list.entries[i];
                if (e.chapter == chapter && e.level != null) any = true;
                if (e.chapter < chapter && !IsCleared(i)) return false;
            }
            return any;
        }

        // ---------- 의뢰 ----------

        /// <summary>챕터가 열려 있고, 챕터 안 앞 의뢰가 클리어면 열린다(챕터의 첫 의뢰는 항상 열림).</summary>
        public bool IsUnlocked(int i)
        {
            if (!ChapterOpen(ChapterOf(i))) return false;
            int pos = IndexInChapter(i);
            if (pos == 0) return true;
            return IsCleared(InChapter(ChapterOf(i))[pos - 1]);
        }

        /// <summary>레벨이 있고 열려 있어야 수락할 수 있다.</summary>
        public bool IsPlayable(int i) => list.entries[i].level != null && IsUnlocked(i);

        public RequestState State(int i)
        {
            if (!IsPlayable(i)) return RequestState.Locked;
            return IsCleared(i) ? RequestState.Clear : RequestState.New;
        }
    }
}
