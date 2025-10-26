using UnityEngine;

public enum CellState { Dead, Player1, Player2 }

public class Cell : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Color deadColor = new Color(0.75f, 0.75f, 0.75f);
    [SerializeField] private Color p1Color = new Color(0.25f, 0.55f, 1f);
    [SerializeField] private Color p2Color = new Color(1f, 0.40f, 0.40f);

    [Header("State")]
    [SerializeField] private CellState state = CellState.Dead;
    public int X { get; private set; }
    public int Y { get; private set; }
    public bool IsAlive => state != CellState.Dead;
    public CellState State => state;

    private GridManager manager;

    private void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        RefreshVisual();
    }

    public void Initialize(GridManager mgr, int x, int y, bool startAlive)
    {
        manager = mgr;
        X = x;
        Y = y;
        state = startAlive ? CellState.Player1 : CellState.Dead;
        RefreshVisual();
    }

    public void SetState(CellState s)
    {
        state = s;
        RefreshVisual();
    }

    public void Kill() => SetState(CellState.Dead);

    private void RefreshVisual()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        switch (state)
        {
            case CellState.Player1: sr.color = p1Color; break;
            case CellState.Player2: sr.color = p2Color; break;
            default: sr.color = deadColor; break;
        }
    }
}
