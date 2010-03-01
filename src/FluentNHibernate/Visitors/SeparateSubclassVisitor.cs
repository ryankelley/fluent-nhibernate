using System;
using System.Collections.Generic;
using System.Linq;
using FluentNHibernate.Mapping.Providers;
using FluentNHibernate.MappingModel.ClassBased;
using FluentNHibernate.Utils;

namespace FluentNHibernate.Visitors
{
    public class SeparateSubclassVisitor : DefaultMappingModelVisitor
    {
        private readonly IList<IMappingProvider> subclassProviders;

        public SeparateSubclassVisitor(IList<IMappingProvider> subclassProviders)
        {
            this.subclassProviders = subclassProviders;
        }

        public override void ProcessClass(ClassMapping mapping)
        {
            var subclasses = FindClosestSubclasses(mapping.Type);

            foreach (var provider in subclasses)
            {
                var subclass = CreateSubclass(mapping);
                var result = provider.GetClassMapping();
                result.ApplyTo(subclass);

                mapping.AddSubclass(subclass);
            }

            base.ProcessClass(mapping);
        }

        public override void ProcessSubclass(SubclassMapping mapping)
        {
            var subclasses = FindClosestSubclasses(mapping.Type);

            foreach (var provider in subclasses)
            {
                var subclass = new SubclassMapping(mapping.SubclassType);
                var result = provider.GetClassMapping();
                result.ApplyTo(subclass);

                mapping.AddSubclass(subclass);
            }

            base.ProcessSubclass(mapping);
        }

        private IEnumerable<IMappingProvider> FindClosestSubclasses(Type type)
        {
            var subclasses = SortByDistanceFrom(type, subclassProviders);

            if (subclasses.Keys.Count == 0)
                return new IMappingProvider[0];

            var lowestDistance = subclasses.Keys.Min();

            return subclasses[lowestDistance];
        }

        private SubclassMapping CreateSubclass(ClassMapping mapping)
        {
            if (mapping.Discriminator == null)
                return new SubclassMapping(SubclassType.JoinedSubclass);

            return new SubclassMapping(SubclassType.Subclass);
        }

        private bool IsMapped(Type type, IEnumerable<IMappingProvider> providers)
        {
            return providers.Any(x => x.Type == type);
        }

        /// <summary>
        /// Takes a type that represents the level in the class/subclass-hiearchy that we're starting from, the parent,
        /// this can be a class or subclass; also takes a list of subclass providers. The providers are then iterated
        /// and added to a dictionary key'd by the types "distance" from the parentType; distance being the number of levels
        /// between parentType and the subclass-type.
        /// 
        /// By default if the Parent type is an interface the level will always be zero. At this time there is no check for
        /// hierarchical interface inheritance.
        /// </summary>
        /// <param name="parentType">Starting point, parent type.</param>
        /// <param name="subProviders">List of subclasses</param>
        /// <returns>Dictionary key'd by the distance from the parentType.</returns>
        private IDictionary<int, IList<IMappingProvider>> SortByDistanceFrom(Type parentType, IEnumerable<IMappingProvider> subProviders)
        {
            var arranged = new Dictionary<int, IList<IMappingProvider>>();

            foreach (var subclassProvider in subProviders)
            {
                var subclassType = subclassProvider.Type;
                var level = 0;

                bool implOfParent = parentType.IsInterface
                    ? DistanceFromParentInterface(parentType, subclassType, ref level)
                    : DistanceFromParentBase(parentType, subclassType.BaseType, ref level);

                if (!implOfParent) continue;

                if (!arranged.ContainsKey(level))
                    arranged[level] = new List<IMappingProvider>();

                arranged[level].Add(subclassProvider);
            }

            return arranged;
        }

        /// <summary>
        /// The evalType starts out as the original subclass. The class hiearchy is only
        /// walked if the subclass inherits from a class that is included in the subclassProviders.
        /// </summary>
        /// <param name="parentType"></param>
        /// <param name="evalType"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        private bool DistanceFromParentInterface(Type parentType, Type evalType, ref int level)
        {
            if (!evalType.HasInterface(parentType)) return false;

            if (!(evalType == typeof(object)) &&
                IsMapped(evalType.BaseType, subclassProviders))
            {
                //Walk the tree if the subclasses base class is also in the subclassProviders
                level++;
                DistanceFromParentInterface(parentType, evalType.BaseType, ref level);
            }

            return true;
        }

        /// <summary>
        /// The evalType is always one class higher in the hiearchy starting from the original subclass. The class 
        /// hiearchy is walked until the IsTopLevel (base class is Object) is met. The level is only incremented if 
        /// the subclass inherits from a class that is also in the subclassProviders.
        /// </summary>
        /// <param name="parentType"></param>
        /// <param name="evalType"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        private bool DistanceFromParentBase(Type parentType, Type evalType, ref int level)
        {
            var evalImplementsParent = false;
            if (evalType == parentType)
                evalImplementsParent = true;

            if (!evalImplementsParent && !(evalType == typeof(object)))
            {
                //If the eval class does not inherit the parent but it is included
                //in the subclassprovides, then the original subclass can not inherit 
                //directly from the parent.
                if (IsMapped(evalType, subclassProviders))
                    level++;
                evalImplementsParent = DistanceFromParentBase(parentType, evalType.BaseType, ref level);
            }

            return evalImplementsParent;
        }
    }
}