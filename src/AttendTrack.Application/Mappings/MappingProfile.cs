using AttendTrack.Application.DTOs;
using AttendTrack.Application.Common;
using AttendTrack.Domain.Entities;
using AutoMapper;

namespace AttendTrack.Application.Mappings;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Employee → EmployeeDto
        CreateMap<Employee, EmployeeDto>()
            .ForCtorParam("employeeId",        opt => opt.MapFrom(s => s.Id.Value))
            .ForCtorParam("role",              opt => opt.MapFrom(s => s.Role.ToString()))
            .ForCtorParam("joinedAt",          opt => opt.MapFrom(s => s.JoinedAt.ToString("dd MMM yyyy")));

        // HikvisionEventLog → HikvisionEventDto (IST formatted times)
        CreateMap<HikvisionEventLog, HikvisionEventDto>()
            .ForCtorParam("deviceLocalTime",
                opt => opt.MapFrom(s => IstClock.FormatIst(s.DeviceLocalTime)));
    }
}

