using System.Collections.Generic;
using AutoMapper;

namespace Backend.Util
{
    public static class AutoMapperExtensions
    {
        public static TDestination MapTo<TDestination>(this object source, IMapper mapper)
        {
            return mapper.Map<TDestination>(source);
        }

        public static IEnumerable<TDestination> MapTo<TDestination>(this IEnumerable<object> source, IMapper mapper)
        {
            return mapper.Map<IEnumerable<TDestination>>(source);
        }
    }
}