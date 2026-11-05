using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Stores;
using Nop.Data;
using Nop.Plugin.Misc.PunchOut.Domain;
using Nop.Plugin.Misc.PunchOut.Services;
using Nop.Services.Authentication;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Tax;
using NUnit.Framework;
using ILogger = Nop.Services.Logging.ILogger;

namespace Nop.Plugin.Misc.PunchOut.Tests;

/// <summary>
/// Tests how PunchOutService handles the customer's shopping cart (issue #8435):
/// a submitted cart must be emptied, and a session started with operation="create" must start with an empty cart
/// </summary>
[TestFixture]
public class PunchOutServiceShoppingCartTests
{
    private const int STORE_ID = 1;

    private Customer _customer;
    private Mock<IShoppingCartService> _shoppingCartService;
    private Mock<ICustomerService> _customerService;
    private Mock<IGenericAttributeService> _genericAttributeService;
    private PunchOutSettings _settings;
    private List<PunchOutLog> _logs;
    private PunchOutService _punchOutService;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // CacheKey reads its default cache time from the static AppSettings singleton
        Singleton<AppSettings>.Instance ??= new AppSettings([new CacheConfig()]);
    }

    [SetUp]
    public void SetUp()
    {
        _customer = new Customer { Id = 42, Email = PunchOutTestData.CustomerEmail, ShippingAddressId = 7 };

        _shoppingCartService = new Mock<IShoppingCartService>();

        _customerService = new Mock<ICustomerService>();
        _customerService.Setup(s => s.GetCustomerByEmailAsync(PunchOutTestData.CustomerEmail)).ReturnsAsync(_customer);
        _customerService.Setup(s => s.GetCustomerRoleIdsAsync(_customer, false)).ReturnsAsync([]);

        // the request has no ShipTo address, so give the customer one with the same (empty) postal code
        var addressService = new Mock<IAddressService>();
        addressService.Setup(s => s.GetAddressByIdAsync(7)).ReturnsAsync(new Address { Id = 7 });

        _genericAttributeService = new Mock<IGenericAttributeService>();

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(s => s.GetCurrentStoreAsync()).ReturnsAsync(new Store { Id = STORE_ID });

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(s => s.GetCurrentCustomerAsync()).ReturnsAsync(_customer);

        var webHelper = new Mock<IWebHelper>();
        webHelper.Setup(s => s.GetStoreLocation(It.IsAny<bool?>())).Returns("https://shop.example.com/");

        var identityRepository = new Mock<IRepository<PunchOutIdentity>>();
        identityRepository.Setup(r => r.Table).Returns(new List<PunchOutIdentity>
        {
            new() { Id = 1, Identity = PunchOutTestData.Identity, SharedSecretHash = PunchOutTestData.SharedSecret }
        }.AsQueryable());

        // keep the PunchOut log in memory, so a failing test can show the logged error
        _logs = [];
        var logRepository = new Mock<IRepository<PunchOutLog>>();
        logRepository
            .Setup(r => r.InsertAsync(It.IsAny<PunchOutLog>(), It.IsAny<bool>()))
            .Callback<PunchOutLog, bool>((log, _) => _logs.Add(log))
            .Returns(Task.CompletedTask);

        _settings = new PunchOutSettings { IsActive = true };

        _punchOutService = new PunchOutService(
            addressService.Object,
            Mock.Of<IAuthenticationService>(),
            Mock.Of<ICurrencyService>(),
            Mock.Of<ICountryService>(),
            _customerService.Object,
            _genericAttributeService.Object,
            Mock.Of<ILocalizationService>(),
            Mock.Of<ILogger>(),
            Mock.Of<IPriceCalculationService>(),
            Mock.Of<IProductAttributeFormatter>(),
            Mock.Of<IProductService>(),
            Mock.Of<IRepository<GenericAttribute>>(),
            _shoppingCartService.Object,
            Mock.Of<IStateProvinceService>(),
            Mock.Of<IStaticCacheManager>(),
            storeContext.Object,
            Mock.Of<ITaxService>(),
            webHelper.Object,
            workContext.Object,
            new PunchOutIdentityService(identityRepository.Object),
            new PunchOutLogService(logRepository.Object),
            _settings);
    }

    private string LoggedErrors => string.Join(Environment.NewLine, _logs.Select(log => log.Error).Where(error => error != null));

    private void VerifyCartCleared(Times times)
    {
        _shoppingCartService.Verify(s => s.ClearShoppingCartAsync(_customer, STORE_ID), times);
    }

    #region PrepareShoppingCartAsync

    [Test]
    public async Task PrepareShoppingCart_Create_ClearsCart()
    {
        await _punchOutService.PrepareShoppingCartAsync(_customer, STORE_ID, PunchOutOperation.Create);

        VerifyCartCleared(Times.Once());
    }

    [TestCase(PunchOutOperation.Edit)]
    [TestCase(PunchOutOperation.Inspect)]
    public async Task PrepareShoppingCart_EditOrInspect_KeepsCart(PunchOutOperation operation)
    {
        await _punchOutService.PrepareShoppingCartAsync(_customer, STORE_ID, operation);

        _shoppingCartService.Verify(s => s.ClearShoppingCartAsync(It.IsAny<Customer>(), It.IsAny<int>()), Times.Never());
    }

    [Test]
    public async Task PrepareShoppingCart_NullCustomer_Throws()
    {
        var act = () => _punchOutService.PrepareShoppingCartAsync(null, STORE_ID, PunchOutOperation.Create);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region ClearSubmittedShoppingCartAsync

    [Test]
    public async Task ClearSubmittedShoppingCart_ClearsCurrentCustomersCartInCurrentStore()
    {
        await _punchOutService.ClearSubmittedShoppingCartAsync();

        VerifyCartCleared(Times.Once());
    }

    #endregion

    #region HandleSetupRequestAsync

    [Test]
    public async Task HandleSetupRequest_Create_ClearsCartAndReturnsStartPage()
    {
        var response = await _punchOutService.HandleSetupRequestAsync(
            PunchOutTestData.SetupRequestXml("create"), new DefaultHttpContext());

        response.Should().Contain("code=\"200\"", LoggedErrors).And.Contain("https://shop.example.com/punchout/start?sessionId=");
        VerifyCartCleared(Times.Once());
    }

    [Test]
    public async Task HandleSetupRequest_Edit_KeepsCart()
    {
        var response = await _punchOutService.HandleSetupRequestAsync(
            PunchOutTestData.SetupRequestXml("edit"), new DefaultHttpContext());

        response.Should().Contain("code=\"200\"", LoggedErrors);
        VerifyCartCleared(Times.Never());
    }

    [TestCase("create", PunchOutOperation.Create)]
    [TestCase("edit", PunchOutOperation.Edit)]
    public async Task HandleSetupRequest_SavesOperationInSession(string operation, PunchOutOperation expected)
    {
        string savedSession = null;
        _genericAttributeService
            .Setup(s => s.SaveAttributeAsync(_customer, PunchOutDefaults.PunchOutSessionAttribute, It.IsAny<string>(), STORE_ID))
            .Callback<BaseEntity, string, string, int>((_, _, value, _) => savedSession = value)
            .Returns(Task.CompletedTask);

        await _punchOutService.HandleSetupRequestAsync(PunchOutTestData.SetupRequestXml(operation), new DefaultHttpContext());

        savedSession.Should().NotBeNull();
        Newtonsoft.Json.JsonConvert.DeserializeObject<PunchOutSession>(savedSession).Operation.Should().Be(expected);
    }

    [Test]
    public async Task HandleSetupRequest_UnsupportedOperation_ReturnsCxmlErrorAndKeepsCart()
    {
        var response = await _punchOutService.HandleSetupRequestAsync(
            PunchOutTestData.SetupRequestXml("delete"), new DefaultHttpContext());

        response.Should().Contain("code=\"400\"");
        VerifyCartCleared(Times.Never());
    }

    [Test]
    public async Task HandleSetupRequest_RestrictedRole_KeepsCart()
    {
        _customerService.Setup(s => s.GetCustomerRoleIdsAsync(_customer, false)).ReturnsAsync([3]);
        _settings.RestrictedCustomerRoleIds.Add(3);

        var response = await _punchOutService.HandleSetupRequestAsync(
            PunchOutTestData.SetupRequestXml("create"), new DefaultHttpContext());

        response.Should().Contain("code=\"400\"");
        VerifyCartCleared(Times.Never());
    }

    #endregion
}
