using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Templates.AR;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Extends the existing AR Template Menu with inventory functionality
/// Allows dragging spawned objects back to the object menu for storage
/// </summary>
public class InventoryScript : MonoBehaviour
{
    [Header("Integration with AR Template Menu")]
    [SerializeField] ARTemplateMenuManager menuManager;
    [SerializeField] Transform inventoryContainer; // Container inom objectMenu för inventory items
    
    [Header("Inventory Settings")]
    [SerializeField] GameObject inventoryItemPrefab; // Prefab för inventory items
    [SerializeField] int maxInventoryItems = 12;
    
    private List<InventoryItem> inventoryItems = new List<InventoryItem>();
    private ObjectSpawner objectSpawner;
    
    [System.Serializable]
    public class InventoryItem
    {
        public GameObject originalPrefab;
        public GameObject uiElement;
        public int prefabIndex;
        
        public InventoryItem(GameObject prefab, GameObject ui, int index)
        {
            originalPrefab = prefab;
            uiElement = ui;
            prefabIndex = index;
        }
    }
    
    void Start()
    {
        InitializeInventorySystem();
    }
    
    void InitializeInventorySystem()
    {
        // Hitta komponenter automatiskt
        if (menuManager == null)
            menuManager = FindFirstObjectByType<ARTemplateMenuManager>();
            
        if (objectSpawner == null)
            objectSpawner = FindFirstObjectByType<ObjectSpawner>();
            
        // Sätt upp inventory container inom befintlig objectMenu
        if (menuManager != null && menuManager.objectMenu != null && inventoryContainer == null)
        {
            CreateInventoryContainer();
        }
        
        // Lyssna på spawn events
        if (objectSpawner != null)
        {
            objectSpawner.objectSpawned += OnObjectSpawned;
        }
        
        Debug.Log("Inventory system integrerat med AR Template Menu");
    }
    
    void CreateInventoryContainer()
    {
        // Skapa en scroll area för inventory inom befintlig objectMenu
        GameObject scrollArea = new GameObject("InventoryScrollArea");
        scrollArea.transform.SetParent(menuManager.objectMenu.transform);
        
        RectTransform scrollRect = scrollArea.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 0.3f); // Tar upp nedre 30% av menyn
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;
        
        ScrollRect scroll = scrollArea.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        
        // Content för inventory items
        GameObject content = new GameObject("InventoryContent");
        content.transform.SetParent(scrollArea.transform);
        
        inventoryContainer = content.transform;
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(0, 1);
        contentRect.pivot = new Vector2(0, 0.5f);
        
        scroll.content = contentRect;
        
        // Lägg till ContentSizeFitter för dynamisk storlek
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        // Lägg till HorizontalLayoutGroup för snyggt layout
        HorizontalLayoutGroup layout = content.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        
        Debug.Log("Inventory container skapad inom AR Template Menu");
    }
    
    void OnObjectSpawned(GameObject spawnedObject)
    {
        // Setup spawnat objekt för inventory funktionalitet
        SetupSpawnedObjectForInventory(spawnedObject);
    }
    
    void SetupSpawnedObjectForInventory(GameObject obj)
    {
        // Lägg till XRGrabInteractable om det saknas
        var grabInteractable = obj.GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
        {
            grabInteractable = obj.AddComponent<XRGrabInteractable>();
        }
        
        // Lyssna på när objektet släpps
        grabInteractable.selectExited.AddListener((args) => {
            CheckForInventoryDrop(obj);
        });
        
        // Lägg till collider om det saknas
        if (obj.GetComponent<Collider>() == null)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                var boxCollider = obj.AddComponent<BoxCollider>();
                boxCollider.size = renderer.bounds.size;
            }
        }
    }
    
    void CheckForInventoryDrop(GameObject droppedObject)
    {
        // Kolla om objektet droppades nära inventory området
        if (menuManager.objectMenu.activeInHierarchy && IsNearInventoryArea(droppedObject))
        {
            AddObjectToInventory(droppedObject);
        }
    }
    
    bool IsNearInventoryArea(GameObject obj)
    {
        // Enkel distans-check - kan förbättras med ray casting
        Camera cam = Camera.main;
        if (cam == null) return false;
        
        Vector3 screenPos = cam.WorldToScreenPoint(obj.transform.position);
        
        // Kolla om objektet är i nedre delen av skärmen där inventory är
        return screenPos.y < Screen.height * 0.4f && screenPos.x > 0 && screenPos.x < Screen.width;
    }
    
    public void AddObjectToInventory(GameObject originalObject)
    {
        if (inventoryItems.Count >= maxInventoryItems)
        {
            Debug.LogWarning("Inventory fullt!");
            return;
        }
        
        // Hitta vilket prefab index detta objekt motsvarar
        int prefabIndex = FindPrefabIndex(originalObject);
        if (prefabIndex == -1)
        {
            Debug.LogWarning("Kunde inte identifiera objekttyp");
            return;
        }
        
        // Skapa inventory UI element
        GameObject inventoryUI = CreateInventoryUIElement(originalObject, prefabIndex);
        
        // Lägg till i inventory
        InventoryItem invItem = new InventoryItem(
            objectSpawner.objectPrefabs[prefabIndex], 
            inventoryUI, 
            prefabIndex
        );
        
        inventoryItems.Add(invItem);
        
        // Förstör det ursprungliga objektet
        Destroy(originalObject);
        
        Debug.Log($"Lagt till {originalObject.name} i inventory ({inventoryItems.Count}/{maxInventoryItems})");
    }
    
    GameObject CreateInventoryUIElement(GameObject originalObject, int prefabIndex)
    {
        // Skapa en enkel knapp för inventory item
        GameObject itemUI = new GameObject($"InventoryItem_{originalObject.name}");
        itemUI.transform.SetParent(inventoryContainer);
        
        RectTransform rect = itemUI.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(80, 80);
        
        // Lägg till Image med objektets färg
        Image image = itemUI.AddComponent<Image>();
        image.color = GetObjectColor(originalObject);
        
        // Lägg till Button för respawn
        Button button = itemUI.AddComponent<Button>();
        button.onClick.AddListener(() => RespawnFromInventory(prefabIndex, itemUI));
        
        return itemUI;
    }
    
    int FindPrefabIndex(GameObject obj)
    {
        if (objectSpawner == null) return -1;
        
        string objName = obj.name.Replace("(Clone)", "").Trim();
        
        for (int i = 0; i < objectSpawner.objectPrefabs.Count; i++)
        {
            if (objectSpawner.objectPrefabs[i].name == objName)
            {
                return i;
            }
        }
        
        return -1;
    }
    
    void RespawnFromInventory(int prefabIndex, GameObject inventoryUI)
    {
        // Använd ARTemplateMenuManager för att sätta spawn index
        if (menuManager != null)
        {
            menuManager.SetObjectToSpawn(prefabIndex);
        }
        
        // Ta bort från inventory
        inventoryItems.RemoveAll(item => item.uiElement == inventoryUI);
        Destroy(inventoryUI);
        
        Debug.Log($"Valde objekt från inventory för spawning: index {prefabIndex}");
    }
    
    Color GetObjectColor(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null)
            renderer = obj.GetComponentInChildren<Renderer>();
            
        if (renderer != null && renderer.material != null && renderer.material.HasProperty("_Color"))
        {
            return renderer.material.color;
        }
        
        return Color.cyan;
    }
    
    void OnDestroy()
    {
        // Städa upp event listeners
        if (objectSpawner != null)
        {
            objectSpawner.objectSpawned -= OnObjectSpawned;
        }
    }
}