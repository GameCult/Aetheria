using System;

public abstract class BaseInspector<T>
{
    protected const int width = 150;
    protected const int labelWidth = 20;
    protected const int toggleWidth = 18;
    protected const int arrowWidth = 22;
    
    public abstract T Inspect(string label, T value, object parent, DatabaseInspector inspectorWindow);
}

public abstract class BaseInspector<T, A> where A : Attribute
{
    protected const int width = 150;
    protected const int labelWidth = 20;
    protected const int toggleWidth = 18;
    protected const int arrowWidth = 22;
    
    public abstract T Inspect(string label, T value, object parent, DatabaseInspector inspectorWindow, A attribute);
}

public abstract class BaseInspector<T, A, B> where A : Attribute where B : Attribute
{
    protected const int width = 150;
    protected const int labelWidth = 20;
    protected const int toggleWidth = 18;
    protected const int arrowWidth = 22;
    
    public abstract T Inspect(string label, T value, object parent, DatabaseInspector inspectorWindow, A attributeA, B attributeB);
}