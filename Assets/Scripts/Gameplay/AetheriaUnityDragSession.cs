using System;

public sealed class AetheriaUnityDragSession
{
    private DragObject _dragObject;
    private Action<DragObject> _endDragCallback;

    public void Begin(DragObject dragObject)
    {
        _dragObject = dragObject;
    }

    public bool TryGetDraggedItem(out ItemDragObject itemDragObject)
    {
        itemDragObject = _dragObject as ItemDragObject;
        return itemDragObject != null;
    }

    public void RegisterTarget(Action<DragObject> onEndDrag)
    {
        _endDragCallback = onEndDrag;
    }

    public void UnregisterTarget()
    {
        _endDragCallback = null;
    }

    public bool End()
    {
        var hadTarget = _endDragCallback != null;
        _endDragCallback?.Invoke(_dragObject);
        _dragObject = null;
        _endDragCallback = null;
        return hadTarget;
    }
}
