using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trine.Analyzer.Tests.TestHelper;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Trine.Analyzer.Tests
{
    [TestClass]
    public class InjectTests : CodeFixVerifier
    {
        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new InjectInstanceCodeFixProvider();
        }
        
        [TestMethod]
        public void Inject_WhenNoConstructor_CreatesConstructor()
        {
            const string before = @"
interface IInterface
{
    void InterfaceMethod();
}

class Class
{
    public void Method()
    {
        IInterface.InterfaceMethod();
    }
}";
            const string after = @"
interface IInterface
{
    void InterfaceMethod();
}

class Class
{
    private readonly IInterface _interface;

    public Class(IInterface interface)
    {
        _interface = interface;
    }

    public void Method()
    {
        _interface.InterfaceMethod();
    }
}";
            VerifyCSharpFix(before, after);
        }

        [TestMethod]
        public void Inject_WhenHasConstructor_UpdatesExistting()
        {
            const string before = @"
interface IInterface
{
    void InterfaceMethod();
}

class Class
{
    private readonly IAnotherService _anotherService;

    public Class(IAnotherService anotherService)
    {
        _anotherService = anotherService;
    }

    public void Method()
    {
        IInterface.InterfaceMethod();
    }
}";
            const string after = @"
interface IInterface
{
    void InterfaceMethod();
}

class Class
{
    private readonly IInterface _interface;
    private readonly IAnotherService _anotherService;

    public Class(IAnotherService anotherService, IInterface interface)
    {
        _anotherService = anotherService;
        _interface = interface;
    }

    public void Method()
    {
        _interface.InterfaceMethod();
    }
}";
            VerifyCSharpFix(before, after);
        }

        [TestMethod]
        public void Inject_WhenHasGenericArguments_UpdatesCorrectly()
        {
            const string before = @"
interface IInterface<T>
{
    void InterfaceMethod();
}

class Class
{
    public void Method()
    {
        IInterface<Class>.InterfaceMethod();
    }
}";
            const string after = @"
interface IInterface<T>
{
    void InterfaceMethod();
}

class Class
{
    private readonly IInterface<Class> _interface;

    public Class(IInterface<Class> interface)
    {
        _interface = interface;
    }

    public void Method()
    {
        _interface.InterfaceMethod();
    }
}";
            VerifyCSharpFix(before, after);
        }
    }
}
