using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GridManager : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int columns = 20;
    [SerializeField] private int rows = 20;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector2 origin = Vector2.zero;

    [Header("Prefabs")]
    [SerializeField] private Cell cellPrefab;

    [Header("Start Fill (classic)")]
    [SerializeField] private bool startAliveRandom = false;
    [Range(0f, 1f)] [SerializeField] private float aliveChance = 0.25f;

    [Header("Run speed")]
    [SerializeField] private float minStepInterval = 0.05f;
    [SerializeField] private float maxStepInterval = 0.60f;
    [SerializeField] private float stepInterval = 0.25f;
    [SerializeField] private Slider speedSlider;

    [Header("PvP")]
    [SerializeField] private bool pvpMode = true;
    [SerializeField] private int seedsPerPlayer = 10;

    [Header("UI (PvP-only)")]
    [SerializeField] private TMP_Text seedsText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button endMatchButton;
    [SerializeField] private GameObject winnerBackground;
    [SerializeField] private Button restartButton;
    [SerializeField] private GameObject tokensButtonsPanel;
    private int selectedTokens = 10;

    [SerializeField] private TokenButtonUI btn5;
    [SerializeField] private TokenButtonUI btn10;
    [SerializeField] private TokenButtonUI btn20;

    [Header("UI Colors")]
    [SerializeField] private Color p1UiColor = new Color(0.25f, 0.55f, 1.00f);
    [SerializeField] private Color p2UiColor = new Color(1.00f, 0.40f, 0.40f);

    public bool IsRunning { get; private set; } = false;
    public bool IsSetupPhase { get; private set; } = false;
    public bool CanEdit => !IsRunning;

    private Cell[,] grid;
    private CellState[,] state;
    private CellState[,] nextState;

    private float timer;
    private int generation;

    private CellState currentPlayer = CellState.Player1;
    private int remainingP1, remainingP2;

    private int scoreP1, scoreP2;
    public bool IsPvPMode => pvpMode;

    private void Start()
    {
        BuildGrid();
        FitCamera();

        if (speedSlider == null) speedSlider = Object.FindAnyObjectByType<Slider>();
        if (speedSlider != null)
        {
            speedSlider.minValue = 0f;
            speedSlider.maxValue = 1f;
            speedSlider.wholeNumbers = false;
            speedSlider.onValueChanged.RemoveAllListeners();
            speedSlider.onValueChanged.AddListener(SetSpeed);
            speedSlider.value = 0.5f;
            SetSpeed(speedSlider.value);
        }

        if (pvpMode) EnterSetupPhase();
        UpdateUI();
        HideResult();
        if (tokensButtonsPanel != null)
            tokensButtonsPanel.SetActive(pvpMode && IsSetupPhase);

        
        var modeButtons = FindAnyObjectByType<ModeButtonsUI>();
        if (modeButtons != null)
        {
            if (pvpMode) modeButtons.HighlightPvP();
            else modeButtons.HighlightClassic();
        }
        HighlightTokenButtons(seedsPerPlayer);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) ToggleRun();
        if (!pvpMode && CanEdit && Input.GetKeyDown(KeyCode.RightArrow)) StepOnce();

        if (CanEdit && Input.GetMouseButtonDown(0))
            HandleClick();

        if (IsRunning)
        {
            timer += Time.deltaTime;
            if (timer >= stepInterval)
            {
                timer = 0f;
                StepSimulation();
            }
        }
    }

    private void UpdateUI()
    {
        if (turnText != null)
        {
            string phase = IsSetupPhase ? "Setup" : (IsRunning ? "Simulation" : "Paused");
            if (IsSetupPhase && pvpMode)
            {
                Color c = (currentPlayer == CellState.Player1) ? p1UiColor : p2UiColor;
                string hex = ColorUtility.ToHtmlStringRGB(c);
                string who = (currentPlayer == CellState.Player1) ? "BLUE" : "RED";
                turnText.text = $"{phase} —\nTurn: <color=#{hex}>{who}</color>";
            }
            else
            {
                turnText.text = phase;
            }
        }

        bool showPvp = pvpMode;

        if (turnText != null) turnText.gameObject.SetActive(showPvp);
        if (seedsText != null) seedsText.gameObject.SetActive(showPvp && IsSetupPhase);
        if (scoreText != null) scoreText.gameObject.SetActive(showPvp);
        if (endMatchButton != null) endMatchButton.gameObject.SetActive(showPvp);
        if (resultText != null && !showPvp) resultText.gameObject.SetActive(false);

        if (showPvp && turnText != null)
        {
            string phase = IsSetupPhase ? "Setup" : (IsRunning ? "Simulation" : "Paused");
            if (IsSetupPhase)
            {
                Color c = (currentPlayer == CellState.Player1) ? p1UiColor : p2UiColor;
                string hex = ColorUtility.ToHtmlStringRGB(c);
                string who = (currentPlayer == CellState.Player1) ? "BLUE" : "RED";
                turnText.text = $"{phase} —\nTurn: <color=#{hex}>{who}</color>";
            }
            else
            {
                turnText.text = phase;
            }
        }

        if (showPvp && seedsText != null)
        {
            seedsText.text =
                $"Tokens:\n<color=#{ColorUtility.ToHtmlStringRGB(p1UiColor)}>BLUE</color>={remainingP1}\n" +
                $"<color=#{ColorUtility.ToHtmlStringRGB(p2UiColor)}>RED</color>={remainingP2}";
        }

        if (showPvp && scoreText != null)
            scoreText.text =
                $"Score:\n<color=#{ColorUtility.ToHtmlStringRGB(p1UiColor)}>BLUE</color>={scoreP1}\n" +
                $"<color=#{ColorUtility.ToHtmlStringRGB(p2UiColor)}>RED</color>={scoreP2}\n" +
                $"Generation: {generation}";

        if (tokensButtonsPanel != null)
            tokensButtonsPanel.SetActive(showPvp && IsSetupPhase);
    }

    private void BuildGrid()
    {
        if (cellPrefab == null)
        {
            Debug.LogError("Cell prefab is not assigned in GridManager!");
            return;
        }

        grid = new Cell[columns, rows];
        state = new CellState[columns, rows];
        nextState = new CellState[columns, rows];

        for (int x = 0; x < columns; x++)
        for (int y = 0; y < rows; y++)
        {
            Vector2 pos = new Vector2(origin.x + x * cellSize, origin.y + y * cellSize);
            var cell = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
            cell.name = $"Cell_{x}_{y}";
            cell.transform.localScale = Vector3.one * cellSize * 0.95f;

            var bc = cell.GetComponent<BoxCollider2D>();
            if (bc != null)
            {
                bc.size = Vector2.one;
                bc.isTrigger = false;
            }

            bool alive = startAliveRandom && (Random.value < aliveChance);
            cell.Initialize(this, x, y, alive);
            grid[x, y] = cell;
            state[x, y] = alive ? CellState.Player1 : CellState.Dead;
        }

        transform.position = new Vector3(-columns * cellSize / 2f, -rows * cellSize / 2f, 0f);
        timer = 0f;
        generation = 0;
        scoreP1 = scoreP2 = 0;
    }

    private void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        cam.orthographic = true;

        float width = columns * cellSize;
        float height = rows * cellSize;

        float sizeByHeight = height / 2f + 1f;
        float aspect = (float)Screen.width / Screen.height;
        float sizeByWidth = (width / aspect) / 2f + 1f;

        cam.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth);
        cam.transform.position = new Vector3(0f, 0f, -10f);
    }

    public void ToggleRun()
    {
        if (IsSetupPhase)
        {
            ExitSetupPhaseAndStart();
            return;
        }

        IsRunning = !IsRunning;
        if (IsRunning) timer = 0f;
        UpdateUI();
    }

    public void StartRun()
    {
        if (IsSetupPhase) ExitSetupPhaseAndStart();
        else { IsRunning = true; timer = 0f; }
        UpdateUI();
    }

    public void StopRun()
    {
        IsRunning = false;
        UpdateUI();
    }

    public void StepOnce()
    {
        if (IsRunning) return;
        StepSimulation();
    }

    public void SetSpeed(float tNormalized)
    {
        tNormalized = Mathf.Clamp01(tNormalized);
        stepInterval = Mathf.Lerp(maxStepInterval, minStepInterval, tNormalized);
    }

    private void EnterSetupPhase()
    {
        IsSetupPhase = true;
        IsRunning = false;
        currentPlayer = CellState.Player1;
        remainingP1 = seedsPerPlayer;
        remainingP2 = seedsPerPlayer;

        ForEachCell(c => { c.Kill(); state[c.X, c.Y] = CellState.Dead; });
        generation = 0;
        scoreP1 = scoreP2 = 0;
        UpdateUI();
        HideResult();
    }

    private void ExitSetupPhaseAndStart()
    {
        IsSetupPhase = false;
        IsRunning = true;
        timer = 0f;
        UpdateUI();
    }

    private void SwitchPlayerIfNeeded()
    {
        if (!IsSetupPhase) return;

        if (currentPlayer == CellState.Player1 && remainingP1 == 0 && remainingP2 > 0)
            currentPlayer = CellState.Player2;
        else if (currentPlayer == CellState.Player2 && remainingP2 == 0 && remainingP1 > 0)
            currentPlayer = CellState.Player1;

        if (remainingP1 == 0 && remainingP2 == 0)
            ExitSetupPhaseAndStart();
        UpdateUI();
    }

    private void HandleClick()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point = new Vector2(world.x, world.y);
        var hit = Physics2D.OverlapPoint(point);
        if (hit == null) return;

        var cell = hit.GetComponent<Cell>();
        if (cell == null) return;

        if (IsSetupPhase && pvpMode)
        {
            HandleSetupClick(cell);
        }
        else
        {
            if (!pvpMode)
            {
                bool willLive = !cell.IsAlive;
                state[cell.X, cell.Y] = willLive ? CellState.Player1 : CellState.Dead;
                cell.SetState(state[cell.X, cell.Y]);
            }
        }
    }

    private void HandleSetupClick(Cell cell)
    {
        var cur = cell.State;

        if (currentPlayer == CellState.Player1)
        {
            if (cur == CellState.Dead && remainingP1 > 0)
            {
                cell.SetState(CellState.Player1);
                state[cell.X, cell.Y] = CellState.Player1;
                remainingP1--;
            }
            else if (cur == CellState.Player1)
            {
                cell.SetState(CellState.Dead);
                state[cell.X, cell.Y] = CellState.Dead;
                remainingP1++;
            }
        }
        else if (currentPlayer == CellState.Player2)
        {
            if (cur == CellState.Dead && remainingP2 > 0)
            {
                cell.SetState(CellState.Player2);
                state[cell.X, cell.Y] = CellState.Player2;
                remainingP2--;
            }
            else if (cur == CellState.Player2)
            {
                cell.SetState(CellState.Dead);
                state[cell.X, cell.Y] = CellState.Dead;
                remainingP2++;
            }
        }
        UpdateUI();
        SwitchPlayerIfNeeded();
    }

    private void StepSimulation()
    {
        int birthsP1 = 0, birthsP2 = 0;

        for (int x = 0; x < columns; x++)
        for (int y = 0; y < rows; y++)
        {
            var cur = state[x, y];
            CountNeighbors(x, y, out int nTotal, out int nP1, out int nP2);

            if (cur != CellState.Dead)
            {
                bool survives = (nTotal == 2 || nTotal == 3);
                nextState[x, y] = survives ? cur : CellState.Dead;
            }
            else
            {
                if (nTotal == 3)
                {
                    if (nP1 > nP2)
                    {
                        nextState[x, y] = CellState.Player1;
                        birthsP1++;
                    }
                    else
                    {
                        nextState[x, y] = CellState.Player2;
                        birthsP2++;
                    }
                }
                else
                {
                    nextState[x, y] = CellState.Dead;
                }
            }
        }

        bool anyChange = false;
        for (int x = 0; x < columns; x++)
        for (int y = 0; y < rows; y++)
        {
            var s = nextState[x, y];
            if (s != state[x, y]) anyChange = true;

            state[x, y] = s;
            grid[x, y].SetState(s);
        }

        scoreP1 += birthsP1;
        scoreP2 += birthsP2;
        generation++;

        int aliveAll = CountAliveAll();

        if (aliveAll == 0)
        {
            ShowWinner();
            return;
        }

        if (!anyChange)
        {
            ShowWinner();
            return;
        }
        UpdateUI();
    }

    private void CountNeighbors(int cx, int cy, out int nTotal, out int nP1, out int nP2)
    {
        nTotal = nP1 = nP2 = 0;

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            int x = cx + dx;
            int y = cy + dy;
            if (x < 0 || y < 0 || x >= columns || y >= rows) continue;

            var s = state[x, y];
            if (s != CellState.Dead)
            {
                nTotal++;
                if (s == CellState.Player1) nP1++;
                else nP2++;
            }
        }
    }

    private int CountAliveAll()
    {
        int count = 0;
        for (int x = 0; x < columns; x++)
        for (int y = 0; y < rows; y++)
            if (state[x, y] != CellState.Dead) count++;
        return count;
    }

    public void RandomFill()
    {
        if (pvpMode)
        {
            EnterSetupPhase();
            return;
        }

        ForEachCell(cell =>
        {
            bool alive = Random.value < aliveChance;
            var s = alive ? CellState.Player1 : CellState.Dead;
            cell.SetState(s);
            state[cell.X, cell.Y] = s;
        });
        generation = 0;
        UpdateUI();
        HideResult();
    }

    public void ClearAll()
    {
        ForEachCell(cell =>
        {
            cell.SetState(CellState.Dead);
            state[cell.X, cell.Y] = CellState.Dead;
        });
        generation = 0;

        if (pvpMode) EnterSetupPhase();
        UpdateUI();
        HideResult();
    }

    public void SetTokens5() => SetTokensByButton(5);
    public void SetTokens10() => SetTokensByButton(10);
    public void SetTokens20() => SetTokensByButton(20);

    private void SetTokensByButton(int value)
    {
        if (!pvpMode) return;

        selectedTokens = Mathf.Clamp(value, 5, 20);
        seedsPerPlayer = selectedTokens;

        StopRun();
        EnterSetupPhase();

        remainingP1 = seedsPerPlayer;
        remainingP2 = seedsPerPlayer;

        if (tokensButtonsPanel != null)
            tokensButtonsPanel.SetActive(true);

        HighlightTokenButtons(seedsPerPlayer);
        UpdateUI();
    }

    private void HighlightTokenButtons(int value)
    {
        if (btn5 != null) btn5.SetActive(value == 5);
        if (btn10 != null) btn10.SetActive(value == 10);
        if (btn20 != null) btn20.SetActive(value == 20);
    }

    private void ShowWinner()
    {
        if (!pvpMode)
        {
            IsRunning = false;
            UpdateUI();
            return;
        }

        string winnerText;
        if (scoreP1 > scoreP2) winnerText = "Winner: BLUE";
        else if (scoreP2 > scoreP1) winnerText = "Winner: RED";
        else
        {
            int aliveP1 = 0, aliveP2 = 0;
            for (int x = 0; x < columns; x++)
                for (int y = 0; y < rows; y++)
                {
                    if (state[x, y] == CellState.Player1) aliveP1++;
                    else if (state[x, y] == CellState.Player2) aliveP2++;
                }

            if (aliveP1 > aliveP2) winnerText = "Winner: BLUE";
            else if (aliveP2 > aliveP1) winnerText = "Winner: RED";
            else winnerText = "Draw";
        }

        if (resultText != null)
        {
            string colorHexP1 = ColorUtility.ToHtmlStringRGB(p1UiColor);
            string colorHexP2 = ColorUtility.ToHtmlStringRGB(p2UiColor);

            string niceWinner = winnerText
                .Replace("BLUE", $"<color=#{colorHexP1}>BLUE</color>")
                .Replace("RED", $"<color=#{colorHexP2}>RED</color>");

            resultText.gameObject.SetActive(true);
            resultText.alignment = TextAlignmentOptions.Center;
            resultText.text = $"{niceWinner}";
            if (winnerBackground != null) winnerBackground.SetActive(true);
            if (restartButton != null) restartButton.gameObject.SetActive(true);
        }

        IsRunning = false;
        UpdateUI();
    }

    private void HideResult()
    {
        if (resultText != null) resultText.gameObject.SetActive(false);
        if (winnerBackground != null) winnerBackground.SetActive(false);
        if (restartButton != null) restartButton.gameObject.SetActive(false);
    }

    public void EnablePvP()
    {
        StopRun();
        pvpMode = true;
        HideResult();
        EnterSetupPhase();
        ClearAll();
        UpdateUI();
    }

    public void EnableClassic()
    {
        StopRun();
        pvpMode = false;
        IsSetupPhase = false;
        HideResult();
        ClearAll();
        UpdateUI();
    }

    public void RestartMatch()
    {
        HideResult();

        if (pvpMode)
        {
            EnterSetupPhase();
        }
        else
        {
            ClearAll();
            IsRunning = false;
        }

        UpdateUI();
    }

    public void EndNow()
    {
        if (!pvpMode) return;
        ShowWinner();
    }

    public void ForEachCell(System.Action<Cell> action)
    {
        if (grid == null) return;
        for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
                action?.Invoke(grid[x, y]);
    }

    public void OnCellEdited(int x, int y, bool isAlive)
    {
        state[x, y] = isAlive ? CellState.Player1 : CellState.Dead;
    }
}
