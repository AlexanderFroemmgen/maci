using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Backend.Data.Persistence.Model;
using Backend.Data.Transfer;
using Backend.Util;

namespace Backend
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Experiment, ExperimentDto>()
                .ForMember(s => s.Status, opt => opt.ResolveUsing<GlobalExperimentStateResolver>());
            CreateMap<ExperimentInstance, ExperimentInstanceDto>()
                .ForMember(s => s.Configuration, opt => opt.ResolveUsing<ConfigurationResolver>());
            CreateMap<Parameter, ParameterDto>()
                .ForMember(p => p.Values, opt => opt.ResolveUsing<ParameterValueResolver>());
            CreateMap<Worker, WorkerDto>();
            CreateMap<Record, RecordDto>().ReverseMap();
            CreateMap<LogMessage, LogMessageDto>().ReverseMap();
        }
    }

    public class GlobalExperimentStateResolver : IValueResolver<Experiment, ExperimentDto, ExperimentStatus>
    {
        public static ExperimentStatus Resolve(Experiment source)
        {
            var status = ExperimentStatus.Pending;

            foreach (var simInstance in source.ExperimentInstances)
            {
                if (simInstance.Status > status)
                {
                    status = simInstance.Status;
                }
            }

            return status;
        }

        public ExperimentStatus Resolve(Experiment source, ExperimentDto destination, ExperimentStatus destMember,
            ResolutionContext context)
        {
            return Resolve(source);
        }
    }

    public class ConfigurationResolver :
        IValueResolver<ExperimentInstance, ExperimentInstanceDto, IDictionary<string, object>>
    {
        public IDictionary<string, object> Resolve(ExperimentInstance source, ExperimentInstanceDto destination,
            IDictionary<string, object> destMember,
            ResolutionContext context)
        {
            return source.ParameterValues.Select(v => v.ParameterValue)
                .ToDictionary(parameterValue => parameterValue.Parameter.Name,
                    parameterValue => ParseUtils.ParseToClosestPossibleValueType(parameterValue.Value));
        }
    }

    public class ParameterValueResolver : IValueResolver<Parameter, ParameterDto, IList<string>>
    {
        public IList<string> Resolve(Parameter source, ParameterDto destination, IList<string> destMember,
            ResolutionContext context)
        {
            return source.Values.Select(i => i.Value).ToList();
        }
    }
}