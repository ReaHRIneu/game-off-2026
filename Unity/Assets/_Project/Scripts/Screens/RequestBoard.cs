using GameOff.Data;

namespace GameOff.Game
{
    public enum RequestState { Locked, New, Clear }

    /// <summary>의뢰 목록 + 진행 저장을 묶어 잠금·수락 가능 여부를 계산한다.</summary>
    public sealed class RequestBoard
    {
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

        /// <summary>앞 의뢰 ★1 이상이면 열린다(첫 의뢰는 항상 열림).</summary>
        public bool IsUnlocked(int i) => i == 0 || Best(i - 1) >= 1;

        /// <summary>레벨이 있고 열려 있어야 수락할 수 있다.</summary>
        public bool IsPlayable(int i) => list.entries[i].level != null && IsUnlocked(i);

        public RequestState State(int i)
        {
            if (!IsPlayable(i)) return RequestState.Locked;
            return Best(i) >= 1 ? RequestState.Clear : RequestState.New;
        }
    }
}
