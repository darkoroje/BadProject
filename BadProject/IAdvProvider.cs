using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThirdParty;

namespace BadProject
{
    public interface IAdvProvider
    {
        Advertisement GetAdv(string webId);
    }
}
