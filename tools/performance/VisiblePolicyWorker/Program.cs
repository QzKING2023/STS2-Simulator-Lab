using SimulatorLab.Core;
try
{
    string input=Console.ReadLine()??throw new InvalidDataException("Missing observation");
    Console.WriteLine(VisibleCombatPolicy.Decide(input));
    return 0;
}
catch(Exception ex)
{
    Console.Error.WriteLine(ex.GetType().Name+": invalid visible-policy input");
    return 1;
}
