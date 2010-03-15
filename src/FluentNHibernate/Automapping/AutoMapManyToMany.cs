using System;
using System.Linq;
using System.Reflection;
using FluentNHibernate.MappingModel;
using FluentNHibernate.MappingModel.ClassBased;
using FluentNHibernate.MappingModel.Collections;

namespace FluentNHibernate.Automapping
{
    public class AutoMapManyToMany : IAutoMapper
    {
        private readonly AutoMappingExpressions expressions;

        public AutoMapManyToMany(AutoMappingExpressions expressions)
        {
            this.expressions = expressions;
        }

        public bool MapsProperty(Member property)
        {
            var type = property.PropertyType;
            if (type.Namespace != "Iesi.Collections.Generic" &&
                type.Namespace != "System.Collections.Generic")
                return false;

            var hasInverse = GetInverseProperty(property) != null;
            return hasInverse;
        }

        private static Member GetInverseProperty(Member property)
        {
            Type type = property.PropertyType;
            var inverseSide = type.GetGenericTypeDefinition()
                .MakeGenericType(property.DeclaringType);

            var argument = type.GetGenericArguments()[0];
            return argument.GetProperties()
                .Where(x => x.PropertyType == inverseSide)
                .Select(x => x.ToMember())
                .FirstOrDefault();
        }

        private CollectionMapping GetCollection(Type type, Member property)
        {
            var collection = new CollectionMapping();

            collection.Initialise(type, property);

            return collection;
        }

        private void ConfigureModel(Member property, CollectionMapping mapping, ClassMappingBase classMap, Type parentSide)
        {
            // TODO: Make the child type safer
            mapping.Relationship = CreateManyToMany(property, property.PropertyType.GetGenericArguments()[0], classMap.Type);
            mapping.ContainingEntityType = classMap.Type;
            mapping.ChildType = property.PropertyType.GetGenericArguments()[0];
            mapping.Member = property;

            SetKey(property, classMap, mapping);

            if (parentSide != property.DeclaringType)
                mapping.Inverse = true;
        }

        private ICollectionRelationshipMapping CreateManyToMany(Member property, Type child, Type parent)
        {
            var mapping = new ManyToManyMapping(property.PropertyType.GetGenericArguments()[0])
            {
                ContainingEntityType = parent
            };

            mapping.AddDefaultColumn(new ColumnMapping() { Name = child.Name + "_id" });

            return mapping;
        }

        private void SetKey(Member property, ClassMappingBase classMap, CollectionMapping mapping)
        {
            var columnName = property.DeclaringType.Name + "_id";

            if (classMap is ComponentMapping)
                columnName = expressions.GetComponentColumnPrefix(((ComponentMapping)classMap).Member) + columnName;

            var key = new KeyMapping();

            key.ContainingEntityType = classMap.Type;
            key.AddDefaultColumn(new ColumnMapping() { Name = columnName });

            mapping.Key = key;
        }

        public void Map(ClassMappingBase classMap, Member property)
        {
            var inverseProperty = GetInverseProperty(property);
            var parentSide = expressions.GetParentSideForManyToMany(property.DeclaringType, inverseProperty.DeclaringType);
            var mapping = GetCollection(classMap.Type, property);

            ConfigureModel(property, mapping, classMap, parentSide);

            classMap.AddCollection(mapping);
        }
    }
}