using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement; 

public class NodePlayerController : MonoBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 5f;
    public GraphNode currentNode; 

    [Header("References")]
    public YoloDetector yoloDetector; 
    public Animator anim; 

    [Header("UI Arrows")]
    public GameObject upArrow;    
    public GameObject downArrow;  
    public GameObject leftArrow;  
    public GameObject rightArrow;

    [Header("Tutorial Flags")]
    public bool isInputLocked = false;

    private bool isMoving = false;
    private Vector3 targetPosition;
    private float inputCooldown = 0.5f;
    private float lastInputTime = 0f;

    void Start()
    {
        int targetID = 0;

        // 1. Cek Override dari GameManager (Prioritas Tertinggi)
        if (GameManager.Instance != null && GameManager.Instance.overrideTargetNodeID != 0)
        {
            targetID = GameManager.Instance.overrideTargetNodeID;
            Debug.Log($"[PlayerController] Menggunakan Override Spawn: {targetID}");
            
            // PENTING: Reset agar tidak nyangkut saat restart game
            GameManager.Instance.overrideTargetNodeID = 0;
        }
        // 2. Cek Save Data Posisi Terakhir
        else if (GameManager.Instance != null)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            targetID = GameManager.Instance.GetSavedPosition(currentScene);
        }

        // 3. EKSEKUSI PEMINDAHAN POSISI (LOGIC PENCARIAN)
        if (targetID != 0)
        {
            GraphNode[] allNodes = FindObjectsByType<GraphNode>(FindObjectsSortMode.None);
            bool found = false;

            foreach (GraphNode node in allNodes)
            {
                if (node.nodeID == targetID)
                {
                    currentNode = node; 
                    found = true;
                    Debug.Log($"[PlayerController] Player dipindahkan ke Node ID {targetID}");
                    break;
                }
            }

            if (!found)
            {
                Debug.LogError($"[PlayerController] Node ID {targetID} tidak ditemukan di scene ini!");
            }
        }

        // 4. SETUP FINAL (POSISI PLAYER & KAMERA)
        if (currentNode != null)
        {
            // A. Pindahkan Player Fisik
            transform.position = currentNode.transform.position;
            SaveCurrentPosition();
            UpdateDirectionArrows(); 

            // B. SETUP KAMERA (LOGIKA BARU)
            // Gunakan FindFirstObjectByType karena Camera.main bisa null saat disabled
            SmartCamera cam = FindFirstObjectByType<SmartCamera>();
            if (cam != null)
            {
                cam.player = this.transform; // Kenalkan player ke kamera

                // CEK: Apakah Node awal ini minta kamera Fixed (Ruangan)?
                if (currentNode.fixedCameraMount != null)
                {
                    // MODE FIXED: Teleport kamera ke mount point
                    cam.SetFixedPosition(currentNode.fixedCameraMount.position);
                }
                else
                {
                    // MODE FOLLOW: Teleport kamera ke player & aktifkan follow
                    cam.SnapToTarget(); 
                    cam.SetFollowMode();
                }
            }
        }
        else
        {
            Debug.LogError("CRITICAL: Player tidak punya Node awal! Cek Inspector.");
        }
    }

    void SaveCurrentPosition()
    {
        if (GameManager.Instance != null && currentNode != null)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            GameManager.Instance.SavePosition(currentScene, currentNode.nodeID);
        }
    }

    void Update()
    {
        if (isMoving) return;
        if (isInputLocked) return;
        if (Time.time - lastInputTime < inputCooldown) return;

        string command = "";

        if (yoloDetector != null && !string.IsNullOrEmpty(yoloDetector.currentDetectedWord))
        {
            command = yoloDetector.currentDetectedWord.Trim().ToUpper();
        }
        
        if (string.IsNullOrEmpty(command) && Input.anyKeyDown)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) command = "ATAS";
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) command = "BAWAH";
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) command = "KIRI";
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) command = "KANAN";
        }

        if (!string.IsNullOrEmpty(command))
        {
            TryMove(command);
        }
    }

    public void TryMove(string direction)
    {
        GraphNode targetNode = null;
        float xParam = 0f;
        float yParam = 0f;

        switch (direction)
        {
            case "ATAS":
                targetNode = currentNode.upNode; xParam = 0f; yParam = 1f; break;
            case "BAWAH":
                targetNode = currentNode.downNode; xParam = 0f; yParam = -1f; break;
            case "KIRI":
                targetNode = currentNode.leftNode; xParam = -1f; yParam = 0f; break;
            case "KANAN":
                targetNode = currentNode.rightNode; xParam = 1f; yParam = 0f; break;
        }

        if (targetNode != null)
        {
            if (anim != null)
            {
                anim.SetFloat("horizontal", xParam);
                anim.SetFloat("vertical", yParam);
            }

            StartCoroutine(MoveRoutine(targetNode));
            lastInputTime = Time.time; 
        }
    }
    
    void UpdateDirectionArrows()
    {
        if (currentNode == null) return;
        void CheckArrow(GameObject arrow, GraphNode target) { if (arrow != null) arrow.SetActive(target != null); }
        CheckArrow(upArrow, currentNode.upNode);
        CheckArrow(downArrow, currentNode.downNode);
        CheckArrow(leftArrow, currentNode.leftNode);
        CheckArrow(rightArrow, currentNode.rightNode);
    }

    void HideAllArrows()
    {
        if (upArrow != null) upArrow.SetActive(false);
        if (downArrow != null) downArrow.SetActive(false);
        if (leftArrow != null) leftArrow.SetActive(false);
        if (rightArrow != null) rightArrow.SetActive(false);
    }

    IEnumerator MoveRoutine(GraphNode nextNode)
    {
        isMoving = true;
        HideAllArrows(); 

        targetPosition = nextNode.transform.position;
        
        while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            yield return null; 
        }

        transform.position = targetPosition;
        currentNode = nextNode; 
        SaveCurrentPosition();

        if (anim != null) 
        {
            anim.SetFloat("horizontal", 0f);
            anim.SetFloat("vertical", 0f);
        }
        
        // Cek apakah Node baru ini tipe Fixed Camera (Dalam Ruangan)
        // Gunakan FindFirstObjectByType karena Camera.main bisa null saat disabled
        SmartCamera cam = FindFirstObjectByType<SmartCamera>();
        if (cam != null)
        {
            if (currentNode.fixedCameraMount != null)
            {
                // Set ke posisi diam
                cam.SetFixedPosition(currentNode.fixedCameraMount.position);
            }
            else
            {
                // Jika null, berarti Outdoor -> Aktifkan Follow
                if (!cam.isFollowing) cam.SetFollowMode();
            }
        }

        NodeStoryTrigger storyTrigger = currentNode.GetComponentInChildren<NodeStoryTrigger>();
        if (storyTrigger != null && storyTrigger.ShouldPlay())
        {
            Debug.Log($"[Story] Playing Event: {storyTrigger.eventID}");
            isInputLocked = true;
            
            // Pause camera and detectors during dialog
            SceneSystemController.PauseForDialog();
            
            DialogManager dm = storyTrigger.GetActiveDialogManager(); 

            if (dm != null)
            {
                foreach (var line in storyTrigger.storySequence)
                {
                    dm.ClearBackground();
                    yield return dm.TypeDialog(line.speakerName, line.textContent, null);
                    
                    yield return new WaitForSeconds(0.1f); 
                    while (dm.IsTyping() || (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space)))
                    {
                        yield return null;
                    }
                }
                dm.HideDialog();
                storyTrigger.MarkAsPlayed();
                
                Debug.Log("[Story] Event completed, resuming systems...");
            }
            
            // Resume camera and detectors after dialog
            SceneSystemController.ResumeAfterDialog();
            
            isInputLocked = false;
        }

        // --- NPC INTERACTION ---
        NPCInteractable npc = currentNode.GetComponent<NPCInteractable>();
        if (npc != null && !npc.isInteractionCompleted)
        {
            // NEW: Load Conversation scene instead of inline handling
            Debug.Log($"[NPC] Loading Conversation scene for {npc.npcName}");
            
            // Create or get ConversationDataHolder
            ConversationDataHolder dataHolder = FindFirstObjectByType<ConversationDataHolder>();
            if (dataHolder == null)
            {
                GameObject holderObj = new GameObject("ConversationDataHolder");
                dataHolder = holderObj.AddComponent<ConversationDataHolder>();
            }
            
            // Set NPC data and return info
            string currentSceneName = SceneManager.GetActiveScene().name;
            dataHolder.SetNPCData(npc, currentSceneName, currentNode.nodeID);
            
            // Save current position
            if (GameManager.Instance != null)
            {
                GameManager.Instance.overrideTargetNodeID = currentNode.nodeID;
            }
            
            // CLEANUP SYSTEM (matching battle scene pattern)
            YoloDetector currentYolo = FindFirstObjectByType<YoloDetector>();
            if (currentYolo != null)
            {
                Debug.Log("Cleaning up Scene1 YOLO before Conversation...");
                currentYolo.gameObject.SetActive(false);
            }
            
            UniversalCamera camScript = FindFirstObjectByType<UniversalCamera>();
            if (camScript != null)
            {
                Debug.Log("Cleaning up Scene1 Camera before Conversation...");
                camScript.gameObject.SetActive(false);
            }
            
            // Load Conversation scene
            SceneManager.LoadScene("Conversation");
            yield break;
        }

        LocalPortal portal = currentNode.GetComponent<LocalPortal>(); 
        ScenePortal scenePortal = currentNode.GetComponent<ScenePortal>(); 

        // CASE A: SCENE PORTAL (Pindah Scene)
        if (scenePortal != null) 
        {
            if (scenePortal.sceneToLoad == "BattleTutorial" && PlayerPrefs.GetInt("BattleTutorialDone", 0) == 1)
            {
                Debug.Log("Tutorial Battle sudah selesai. Portal non-aktif.");
                yield break;
            }

            YoloDetector currentYolo = FindFirstObjectByType<YoloDetector>();
            if (currentYolo != null) currentYolo.gameObject.SetActive(false);

            UniversalCamera camScript = FindFirstObjectByType<UniversalCamera>();
            if (camScript != null) camScript.gameObject.SetActive(false); 

            Debug.Log($"[PORTAL] Pindah ke scene: {scenePortal.sceneToLoad}");
            
            // --- SIMPAN DATA SEBELUM PINDAH SCENE ---
            string currentSceneName = SceneManager.GetActiveScene().name;
            
            if (scenePortal.isBattleEncounter)
            {
                // Battle Encounter: Simpan ke BattleDataHolder
                BattleDataHolder dataHolder = FindFirstObjectByType<BattleDataHolder>();
                if (dataHolder == null)
                {
                    GameObject holderObj = new GameObject("BattleDataHolder");
                    dataHolder = holderObj.AddComponent<BattleDataHolder>();
                }
                
                int returnNode = (scenePortal.spawnAtNodeID != 0) ? scenePortal.spawnAtNodeID : currentNode.nodeID;
                dataHolder.SetBattleData(currentSceneName, returnNode);
                
                // Also set override as backup
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.overrideTargetNodeID = returnNode;
                }
            }
            else
            {
                // Pintu biasa: Pergi ke TUJUAN di map sebelah
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.overrideTargetNodeID = scenePortal.spawnAtNodeID;
                }
            }
            // --------------------------------------------------------------------

            SceneManager.LoadScene(scenePortal.sceneToLoad);
            yield break;
        }

        // CASE B: LOCAL PORTAL (Teleport dalam satu scene)
        else if (portal != null && portal.destinationNode != null)
        {
            currentNode = portal.destinationNode;
            transform.position = currentNode.transform.position;
            SaveCurrentPosition();

            if (cam != null)
            {
                if (portal.followPlayerAtDestination)
                {
                    cam.SnapToTarget();
                    cam.SetFollowMode();
                }
                else if (portal.newCameraMount != null)
                {
                    CameraZone destinationZone = portal.newCameraMount.GetComponent<CameraZone>();
                    if (destinationZone != null)
                    {
                        cam.SetZone(destinationZone);
                        Debug.Log($"[Portal] Camera set to zone: {destinationZone.zoneName}");
                    }
                    else
                    {
                        cam.SetFixedPosition(portal.newCameraMount.position);
                    }
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
        
        UpdateDirectionArrows(); 
        yield return new WaitForSeconds(0.1f);
        isMoving = false;
    }
}