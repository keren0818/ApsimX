using System.Collections.Generic;
using System.Linq;
using APSIM.Shared.Documentation;
using Models;
using Models.Core;
using Models.Functions;
using Models.PMF.Organs;

namespace APSIM.Documentation.Models.Types
{

    /// <summary>
    /// Documentation class for generic models
    /// </summary>
    public class DocReproductiveOrgan : DocGeneric
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DocReproductiveOrgan" /> class.
        /// </summary>
        public DocReproductiveOrgan(IModel model): base(model) {}

        /// <summary>
        /// Document the model.
        /// </summary>
        public override List<ITag> Document(int heading = 0)
        {
            List<ITag> tags = base.Document(heading);

            // Document Constants
            var constantTags = new List<ITag>();
            foreach (var constant in model.FindAllChildren<Constant>())
                constantTags.AddRange(AutoDocumentation.Document(constant));
            tags.Add(new Section("Constants", constantTags));

            // Document everything else.
            List<ITag> childrenTags = new();
            foreach (var child in model.Children.Where(child => !(child is Constant) && !(child is Memo)))
                childrenTags.AddRange(AutoDocumentation.Document(child));
            tags.Add(new Section("Children", childrenTags));

            return tags;
        }
    }
}
