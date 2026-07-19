using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Features.Status.Services;

public static class OutageHealthObservationBuilder
{
    public static List<OutageHealthObservation> FromSnapshot(PublicStatusSnapshotDto snapshot)
    {
        var observations = new List<OutageHealthObservation>();

        foreach (var group in snapshot.Groups)
        {
            if (group.Group == StatusComponentGroup.Overall)
            {
                continue;
            }

            if (group.Group == StatusComponentGroup.DataStores)
            {
                foreach (var instance in group.Instances)
                {
                    observations.Add(
                        new OutageHealthObservation
                        {
                            Scope = OutageWindowScope.Instance,
                            Group = group.Group,
                            InstanceId = instance.InstanceId,
                            Status = instance.Status,
                            GroupStatus = group.Status,
                        }
                    );
                }

                continue;
            }

            observations.Add(
                new OutageHealthObservation
                {
                    Scope = OutageWindowScope.Group,
                    Group = group.Group,
                    InstanceId = null,
                    Status = group.Status,
                    GroupStatus = group.Status,
                }
            );

            if (group.Status != PublicHealthStatus.Unhealthy)
            {
                foreach (var instance in group.Instances.Where(i =>
                             i.Status == PublicHealthStatus.Unhealthy
                         ))
                {
                    observations.Add(
                        new OutageHealthObservation
                        {
                            Scope = OutageWindowScope.Instance,
                            Group = group.Group,
                            InstanceId = instance.InstanceId,
                            Status = instance.Status,
                            GroupStatus = group.Status,
                        }
                    );
                }
            }
        }

        return observations;
    }
}
