using Adv;
using BadProject;
using Moq;
using System;
using Xunit;

namespace Tests
{
    public class AdvertisementServiceTests : IDisposable
    {
        private Mock<IAdvProvider> mainProvider;
        private Mock<IAdvProvider> backupProvider;
        private AdvertisementService? service = null;

        // there could be more tests, e.g. how long do timeouts take, requests from multiple threads etc
        // but just putting in a few as an example

        public AdvertisementServiceTests()
        {
            mainProvider = new Mock<IAdvProvider>();
            backupProvider = new Mock<IAdvProvider>();
            service = new AdvertisementService(mainProvider.Object, backupProvider.Object);
            service.StartService();
        }

        public void Dispose()
        {
            if (service != null)
                service.StopService();
        }

        [Fact]
        public void PrimaryProviderCaches()
        {
            // arrange
            mainProvider.Setup(x => x.GetAdv("10")).Returns(new ThirdParty.Advertisement { WebId = "10", Description = "Advertisement 10", Name = "Adv 10" });
            // act
            var adv = service!.GetAdvertisement("10"); // should call provider
            var adv1 = service!.GetAdvertisement("10"); // should get from cache
            // assert
            Assert.True("Adv 10" == adv.Name);
            Assert.True(adv1.Name == adv.Name);
            mainProvider.Verify(x => x.GetAdv("10"), Times.Once);
        }

        [Fact]
        public void RequestForTwoAdvertisementsWorksCorrectly()
        {
            // arrange
            mainProvider.Setup(x => x.GetAdv("1")).Returns(new ThirdParty.Advertisement { WebId = "1", Description = "Advertisement 1", Name = "Adv 1" });
            mainProvider.Setup(x => x.GetAdv("2")).Returns(new ThirdParty.Advertisement { WebId = "2", Description = "Advertisement 2", Name = "Adv 2" });
            // act
            var adv1 = service!.GetAdvertisement("1");
            var adv2 = service!.GetAdvertisement("2");
            // assert
            Assert.True("Adv 1" == adv1.Name);
            Assert.True("Adv 2" == adv2.Name);
            mainProvider.Verify(x => x.GetAdv("1"), Times.Once);
            mainProvider.Verify(x => x.GetAdv("2"), Times.Once);
        }

        [Fact]
        public void SwitchesToBackupProvider()
        {
            // arrange
            backupProvider.Setup(x => x.GetAdv("3")).Returns(new ThirdParty.Advertisement { WebId = "3", Description = "Advertisement 3", Name = "Adv 3" });
            mainProvider.Setup(x => x.GetAdv(It.IsAny<string>())).Throws(new Exception("Fake exception"));
            // act
            var adv = service!.GetAdvertisement("3"); // should call secondary provider
            var adv1 = service!.GetAdvertisement("3"); // should get from cache
            // assert
            Assert.True("Adv 3" == adv.Name);
            Assert.True(adv1.Name == adv.Name);
            mainProvider.Verify(x => x.GetAdv("3"), Times.Exactly(3)); // no configuration, so default retry count is 3
            backupProvider.Verify(x => x.GetAdv("3"), Times.Once);
        }
    }
}