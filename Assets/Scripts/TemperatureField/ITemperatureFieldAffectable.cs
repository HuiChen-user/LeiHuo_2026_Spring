namespace LeiHuo.Gameplay.TemperatureField
{
    public interface ITemperatureFieldAffectable
    {
        void OnEnterTemperatureField(TemperatureFieldContext context);
        void OnStayTemperatureField(TemperatureFieldContext context);
        void OnExitTemperatureField(TemperatureFieldContext context);
    }
}
