namespace FairScience.Factories;

public interface IFactory<out TInstance, in TParameters>
{
    TInstance GetInstance(TParameters parameters);
}

