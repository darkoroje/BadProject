using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThirdParty;

namespace BadProject
{
    // this class will be injected into AdvertismentService, it isolates the service
    // from NoSqlAdvProvider which cannot be changed
    public class NoSqlAdvProviderWrapper : IAdvProvider
    {
        private NoSqlAdvProvider provider = null;

        public Advertisement GetAdv(string webId)
        {
            if (provider == null)
                provider = new NoSqlAdvProvider();
            return provider.GetAdv(webId);
        }
    }
}
