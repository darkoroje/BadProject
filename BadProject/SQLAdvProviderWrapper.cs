using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThirdParty;

namespace BadProject
{
    // this class insulates AdvertismentService from SQLAdvProvider, which cannot be changed

    public class SQLAdvProviderWrapper : IAdvProvider
    {
        public Advertisement GetAdv(string webId)
        {
            return SQLAdvProvider.GetAdv(webId);
        }
    }
}
